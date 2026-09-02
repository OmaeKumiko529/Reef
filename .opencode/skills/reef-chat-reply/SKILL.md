---
name: reef-chat-reply
description: 飞书群聊回复身份规则。当在飞书群聊中回复消息、以已登录用户 DeepSeek娘 的名义发消息、或 @机器人触发后需要回复时使用。要求用 lark-cli 的 --as user 身份而非 --as bot。
---

# 飞书群聊回复身份规则

在「暗礁」项目中，agent 通过飞书与团队交互时，遵循以下身份规则。

## 核心规则

1. **群聊发消息/回复：必须用已登录用户身份（DeepSeek娘），而不是机器人。**
   - 用 `--as user`，不要用 `--as bot`。
   - 回复命令示例：
     ```bash
     lark-cli im +messages-reply --message-id <om_xxx> --as user --markdown "回复内容"
     ```
   - 发送命令示例：
     ```bash
     lark-cli im +messages-send --chat-id <oc_xxx> --as user --markdown "内容"
     ```

2. **触发仍是 @机器人，但回复以 DeepSeek娘 名义发出。**
   - 飞书机制：只有机器人能接收"被@"事件，所以触发入口是 @机器人。
   - 但回复时用 `--as user`，群里看到的是 DeepSeek娘 在说话，而不是机器人。

## 注意事项

- `--as user` 需要 `im:message.send_as_user` scope（已由用户授权）。
- 用 user 身份发消息时，DeepSeek娘 必须在目标群里，否则会报 "not in chat"。
- 私聊（p2p）回复同样用 `--as user`。
- 回复内容用 `--markdown`（含列表/标题时）或 `--text`（纯文本）。
