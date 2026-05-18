# Windows Companion A2UI Support Plan

## Summary

Add Windows A2UI support by making the Windows companion a gateway node for the
`canvas` capability, then render the gateway-hosted A2UI surface in a new WinUI
Canvas page using WebView2.

Windows should follow the current macOS/iOS protocol model: the gateway
advertises `pluginSurfaceUrls.canvas`, the app resolves it to
`/__openclaw__/a2ui/?platform=windows`, and agents drive the surface through
`node.invoke` commands such as `canvas.a2ui.push` and `canvas.a2ui.reset`.

## Key Changes

- Add a `Canvas` navigation destination/page in the Windows shell.
  - Use WebView2 embedded in the existing WinUI app.
  - Auto-load A2UI when the gateway advertises `pluginSurfaceUrls.canvas`.
  - Show clear empty/error states when disconnected, unpaired, or when the
    canvas plugin surface is missing.
- Add a Windows node connection alongside the existing operator connection.
  - Keep the existing operator WebSocket for chat, approvals, sessions, logs,
    and settings.
  - Add a node-role WebSocket session using the same gateway URL/token/device
    identity store.
  - Connect with `role: "node"`, `client.mode: "node"`, `platform: "windows"`,
    `caps: ["canvas"]`, and commands:
    - `canvas.present`
    - `canvas.hide`
    - `canvas.navigate`
    - `canvas.eval`
    - `canvas.snapshot`
    - `canvas.a2ui.push`
    - `canvas.a2ui.pushJSONL`
    - `canvas.a2ui.reset`
- Implement the node invoke handler.
  - Handle `node.invoke.request` events.
  - Return results through `node.invoke.result`.
  - Support `canvas.a2ui.push` by accepting either `{ "messages": [...] }` or
    legacy `{ "jsonl": "..." }`.
  - Validate JSONL as A2UI v0.8 only: `beginRendering`, `surfaceUpdate`,
    `dataModelUpdate`, `deleteSurface`.
  - Support `canvas.a2ui.reset` by calling `globalThis.openclawA2UI.reset()`
    inside WebView2.
  - Support `canvas.eval` and `canvas.navigate` against the WebView2 page.
  - Defer `canvas.snapshot` to a later pass unless WebView2 capture is
    straightforward and testable in the same session.
- Add trusted WebView2 host behavior.
  - Resolve A2UI URL from `pluginSurfaceUrls.canvas` plus
    `__openclaw__/a2ui/?platform=windows`.
  - Use `node.pluginSurface.refresh` when the current surface URL is absent or
    expired.
  - Restrict automatic loading to gateway-advertised A2UI URLs.
  - Bridge A2UI action messages back to native only from the trusted A2UI URL.
  - Do not pass tokens in query strings or expose credentials to page
    JavaScript.
- Update settings/status surfaces.
  - Add Canvas/A2UI readiness to Home or Devices.
  - Add a Settings toggle for enabling the Windows Canvas node, default enabled.
  - Show node pairing state separately from operator connection state when
    needed.

## Test Plan

- Unit tests:
  - A2UI URL resolver converts a canvas plugin surface URL into
    `/__openclaw__/a2ui/?platform=windows`.
  - Node connect payload advertises `role=node`, `caps=["canvas"]`, and the
    expected command allowlist.
  - `node.invoke.request` dispatches to the correct canvas/A2UI handler and
    sends `node.invoke.result`.
  - A2UI JSONL validation accepts v0.8 messages and rejects unsupported
    `createSurface`.
  - WebView host blocks untrusted navigation/action bridge sources.
- Windows app tests:
  - Navigation service includes the new `Canvas` page.
  - Canvas page shows disconnected, missing-host, loading, ready, and error
    states.
  - Existing chat/session/log filtering tests remain unchanged.
- Manual VM smoke:
  - Start the current gateway.
  - Connect the Windows app as operator and node.
  - Approve node pairing if requested.
  - Confirm `openclaw nodes list` shows the Windows node with `canvas`.
  - Run
    `openclaw nodes canvas a2ui push --node <windows-node> --text "Hello from A2UI"`.
  - Verify the Windows Canvas page renders the A2UI content.

## Assumptions

- First implementation uses an in-app `Canvas` page, not a separate floating
  panel.
- A2UI remains gateway-hosted by the Canvas plugin; Windows should not bundle or
  fork its own renderer.
- Windows follows the current protocol and uses `pluginSurfaceUrls.canvas` plus
  `node.pluginSurface.refresh`; no deprecated `canvasHostUrl` fallback.
- WebView2 is the native Windows rendering surface for A2UI.
- The operator connection remains separate from the new node connection so
  existing chat, approvals, sessions, and logs behavior stays stable.
