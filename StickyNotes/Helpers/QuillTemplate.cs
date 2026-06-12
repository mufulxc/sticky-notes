namespace StickyNotes.Helpers;

/// <summary>
/// Quill 富文本编辑器的 HTML 模板（嵌入 WebView2）
/// </summary>
public static class QuillTemplate
{
    public static string GetHtml() => """
<!DOCTYPE html>
<html><head><meta charset="utf-8">
<link href="https://cdn.quilljs.com/1.3.7/quill.snow.css" rel="stylesheet">
<style>
  * { margin:0; padding:0; box-sizing:border-box; }
  html, body { height:100%; overflow:hidden; }
  body { font-family:'Segoe UI','Microsoft YaHei',sans-serif; font-size:14px; }
  #editor { height:100%; display:flex; flex-direction:column; }
  #editor .ql-container { flex:1; overflow-y:auto; }
  /* Quill 工具栏精简样式 */
  .ql-toolbar { border:none !important; border-bottom:1px solid #e0e0d8 !important;
                background:#fafaf5; padding:1px 6px !important; flex-shrink:0; }
  .ql-toolbar .ql-formats { margin-right:6px !important; }
  .ql-toolbar button { width:24px !important; height:20px !important; }
  .ql-toolbar .ql-picker { height:20px !important; }
  .ql-toolbar .ql-picker-label { padding:1px 4px !important; line-height:18px !important; }
  .ql-container { border:none !important; font-size:14px; flex:1; display:flex; flex-direction:column; }
  .ql-editor { padding:10px 12px; line-height:1.6; flex:1; min-height:100%; }
  .ql-editor.ql-blank::before { color:#aaa; font-style:normal; }
  /* 修复 Quill 颜色下拉 */
  .ql-snow .ql-picker.ql-color-picker .ql-picker-label { padding:2px 4px; }
  /* 自定义确认弹窗 */
  #confirm-overlay { display:none; position:absolute; top:0; left:0; right:0; bottom:0;
    background:rgba(0,0,0,0.3); z-index:100; justify-content:center; align-items:center; }
  #confirm-box { background:#fff; border-radius:8px; padding:20px 24px;
    box-shadow:0 4px 20px rgba(0,0,0,0.2); text-align:center; max-width:260px; }
  #confirm-box p { margin:0 0 14px 0; font-size:13px; color:#333; }
  #confirm-box button { margin:0 6px; padding:5px 20px; border:none; border-radius:4px;
    font-size:13px; cursor:pointer; }
  #confirm-ok { background:#e55; color:#fff; }
  #confirm-cancel { background:#eee; color:#333; }
</style>
</head><body>
<div id="toolbar">
  <span class="ql-formats">
    <button class="ql-bold" title="加粗"></button>
    <button class="ql-italic" title="斜体"></button>
    <button class="ql-underline" title="下划线"></button>
    <select class="ql-color" title="字体颜色"></select>
  </span>
  <span class="ql-formats">
    <button class="ql-divider" title="分隔线" onclick="insertDivider()">―</button>
    <button class="ql-link" title="链接" onclick="insertLink()">🔗</button>
    <button class="ql-clean" title="清除格式"></button>
  </span>
  <span class="ql-formats">
    <button title="清空全部" onclick="clearAll()" style="color:#e55;font-weight:bold;font-size:16px;">✕</button>
  </span>
</div>
<div id="editor"></div>
<div id="confirm-overlay" onkeydown="if(event.key==='Escape')closeConfirm();if(event.key==='Enter')doClearAll();"><div id="confirm-box">
  <p>确定清空全部内容？</p>
  <button id="confirm-cancel" onclick="closeConfirm()">取消 (Esc)</button>
  <button id="confirm-ok" onclick="doClearAll()" autofocus>清空 ↵</button>
</div></div>

<script src="https://cdn.quilljs.com/1.3.7/quill.min.js"></script>
<script>
// ═══ Quill 初始化 ═══
var quill = new Quill('#editor', {
  modules: {
    toolbar: '#toolbar',
    clipboard: { matchVisual: false }
  },
  placeholder: '在此输入...',
  theme: 'snow'
});

// ═══ 自定义分隔线 Blot ═══
var BlockEmbed = Quill.import('blots/block/embed');
class DividerBlot extends BlockEmbed {}
DividerBlot.blotName = 'divider';
DividerBlot.tagName = 'hr';
Quill.register(DividerBlot);

// ═══ 自动识别链接 ═══
quill.clipboard.addMatcher(Node.TEXT_NODE, function(node, delta) {
  var text = node.data;
  var urlRegex = /https?:\/\/[^\s]+/g;
  var ops = [];
  var lastIndex = 0;
  var match;
  while ((match = urlRegex.exec(text)) !== null) {
    if (match.index > lastIndex)
      ops.push({ insert: text.slice(lastIndex, match.index) });
    ops.push({ insert: match[0], attributes: { link: match[0] } });
    lastIndex = match.index + match[0].length;
  }
  if (lastIndex < text.length)
    ops.push({ insert: text.slice(lastIndex) });
  return ops.length ? { ops: ops } : delta;
});

// ═══ 内容变更 → C# ═══
var timer = null, saveTimer = null;
// 点击编辑器任意空白处 → 聚焦光标到末尾
document.querySelector('.ql-editor').addEventListener('click', function(e) {
  if (!quill.getSelection()) {
    quill.setSelection(quill.getLength(), 0);
  }
});
quill.on('text-change', function() {
  clearTimeout(timer);
  clearTimeout(saveTimer);
  // 50ms 批处理：合并连续输入，同步内存
  timer = setTimeout(function() {
    try { window.chrome.webview.postMessage({
      type: 'contentChanged',
      html: quill.root.innerHTML
    }); } catch(e) {}
    // 2秒无新输入 → 自动写盘
    saveTimer = setTimeout(function() {
      try { window.chrome.webview.postMessage({
        type: 'saveNow'
      }); } catch(e) {}
    }, 2000);
  }, 50);
});

// ═══ C# 调用函数 ═══
// setContent 只在切文件夹时被调用，此时用户不在输入，无需抑制
function setContent(html) {
  clearTimeout(timer);
  clearTimeout(saveTimer);
  quill.root.innerHTML = html || '';
}
function getContent() { return quill.root.innerHTML; }
function getText() { return quill.getText(); }
function setBold() { quill.format('bold', !quill.getFormat().bold); }
function insertDivider() {
  var range = quill.getSelection();
  var index = range ? range.index : quill.getLength();
  quill.insertEmbed(index, 'divider', true);
  quill.setSelection(index + 1, 0);
}
var _clearCallback = null;
function showConfirm(msg, cb) { _clearCallback = cb; document.getElementById('confirm-overlay').style.display='flex'; setTimeout(function(){ document.getElementById('confirm-ok').focus(); }, 50); }
function closeConfirm() { document.getElementById('confirm-overlay').style.display='none'; _clearCallback=null; }
function doClearAll() { quill.setText(''); closeConfirm(); }
function clearAll() { showConfirm('确定清空全部内容？', function(){ quill.setText(''); }); }
function insertLink() {
  var url = prompt('输入链接地址：', 'https://');
  if (url) {
    var range = quill.getSelection();
    var index = range ? range.index : quill.getLength();
    quill.insertText(index, url, 'link', url);
    quill.insertText(index + url.length, ' ');
  }
}
</script>
</body></html>
""";
}
