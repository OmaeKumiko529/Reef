import { spawn } from "node:child_process";
import { homedir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = __dirname;

const NPM = join(homedir(), "AppData", "Roaming", "npm", "node_modules");
const LARK_CLI = join(NPM, "@larksuite", "cli", "bin", "lark-cli.exe");
const OPENCODE = join(NPM, "opencode-ai", "bin", "opencode.exe");

const BOT_OPEN_ID = "ou_8f57347fce832a7117339c1519924be8";
const MODEL = "deepseek/deepseek-v4-pro";
const DOC_TOKEN = "Qi1mwqQSHitfx4kaHWrcCgzPndd";

const seen = new Set();
const sessions = new Map();
let queue = [];
let busy = false;

const consume = spawn(
  LARK_CLI,
  ["event", "consume", "im.message.receive_v1", "--as", "bot"],
  { stdio: ["pipe", "pipe", "pipe"] }
);

let buf = "";
consume.stdout.on("data", (c) => {
  buf += c.toString("utf8");
  let i;
  while ((i = buf.indexOf("\n")) >= 0) {
    const line = buf.slice(0, i).trim();
    buf = buf.slice(i + 1);
    if (line) onEvent(line);
  }
});
consume.stderr.on("data", (c) => process.stderr.write(c));
consume.on("exit", (code) => {
  console.error(`[feishu-agent] consume exited code=${code}, shutting down`);
  process.exit(code ?? 1);
});

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
function shutdown() {
  try { consume.stdin.end(); } catch {}
  setTimeout(() => process.exit(0), 1000).unref();
}

function onEvent(line) {
  let ev;
  try { ev = JSON.parse(line); } catch { return; }
  const { chat_id, chat_type, message_id, content, mentions, sender_type } = ev;
  if (!message_id || !chat_id) return;
  if (sender_type === "bot") return;
  if (seen.has(message_id)) return;

  let shouldReply = chat_type === "p2p";
  if (chat_type === "group") {
    shouldReply = Array.isArray(mentions) && mentions.some((m) => m.id === BOT_OPEN_ID);
  }
  if (!shouldReply) return;

  seen.add(message_id);
  console.error(`[feishu-agent] queued ${message_id} (${chat_type})`);
  queue.push(ev);
  drain();
}

async function drain() {
  if (busy) return;
  busy = true;
  while (queue.length) {
    const ev = queue.shift();
    await handle(ev);
  }
  busy = false;
}

function replyBot(messageId, text) {
  const r = spawn(
    LARK_CLI,
    ["im", "+messages-reply", "--message-id", messageId, "--as", "bot", "--text", text],
    { stdio: "ignore" }
  );
  r.on("error", (e) => console.error(`[feishu-agent] reply failed: ${e.message}`));
}

function handle(ev) {
  const { chat_id, chat_type, message_id, content } = ev;
  const sessionId = sessions.get(chat_id);

  replyBot(message_id, "收到，正在处理…");

  const prompt = [
    "你是「暗礁」游戏项目的 AI 助手，通过飞书接入，此刻用户在飞书里向你发消息，需要你回复。",
    "",
    "用户消息内容如下：",
    '"""',
    content,
    '"""',
    "",
    "请理解这条消息的意图，生成一个简洁、得体、切题的回复。",
    "",
    `回复目标：chat_id=${chat_id}，需要回复的消息 message_id=${message_id}，chat_type=${chat_type}。`,
    "",
    "回复身份规则（重要）：以已登录用户 DeepSeek娘 的身份回复，务必用 --as user，不要用 --as bot。参见 reef-chat-reply skill。",
    "回复命令示例：",
    `lark-cli im +messages-reply --message-id ${message_id} --as user --text "你的回复内容"`,
    "",
    "要求：",
    "1. 回复简短直接，除非用户明确要求详细。",
    "2. 若用户问到项目/文档内容，可先用 `lark-cli docs +fetch --doc \"" + DOC_TOKEN + "\" --doc-format markdown` 读取总纲获取上下文，再回答。",
    "3. 若回复需要列表/标题，改用 --markdown；纯文本用 --text。",
    "4. 完成后务必用上面的回复命令把结果发出去，不要只在终端打印。",
  ].join("\n");

  const args = ["run", "--format", "json", "--model", MODEL, "--agent", "reef-feishu", "--dir", PROJECT_DIR, "--auto"];
  if (sessionId) {
    args.push("--session", sessionId);
    console.error(`[feishu-agent] continue session ${sessionId} for ${chat_id}`);
  }
  args.push(prompt);

  return new Promise((resolve, reject) => {
    console.error(`[feishu-agent] handling ${message_id}...`);
    const p = spawn(OPENCODE, args, { stdio: ["ignore", "pipe", "pipe"] });
    let out = "";
    p.stdout.on("data", (c) => { out += c.toString("utf8"); });
    p.stderr.on("data", (c) => process.stderr.write(c));
    p.on("exit", (code) => {
      const sid = extractSessionId(out);
      if (sid && !sessions.has(chat_id)) {
        sessions.set(chat_id, sid);
        console.error(`[feishu-agent] new session ${sid} for ${chat_id}`);
      }
      console.error(`[feishu-agent] opencode exit=${code} for ${message_id}`);
      if (code !== 0) {
        replyBot(message_id, "抱歉，处理失败：模型服务暂时不可用（可能额度不足或网络异常），请稍后重试。");
      }
      resolve();
    });
    p.on("error", reject);
  });
}

function extractSessionId(out) {
  for (const line of out.split("\n")) {
    const t = line.trim();
    if (!t || !t.startsWith("{")) continue;
    try {
      const obj = JSON.parse(t);
      if (obj.sessionID) return obj.sessionID;
    } catch {}
  }
  return null;
}

console.error("[feishu-agent] listening (im.message.receive_v1, bot identity)");
