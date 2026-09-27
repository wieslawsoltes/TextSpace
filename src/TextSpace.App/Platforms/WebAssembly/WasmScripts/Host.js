/* TextSpace platform bridge. No document data is transmitted to a server. */
(() => {
  'use strict';
  const MAX_FILE = 32 * 1024 * 1024;
  const encoder = new TextEncoder();
  let databasePromise;
  let lastSnapshot = 0;
  let latestDocumentId = '';
  let pendingRecovery = null;
  let isReady = false;
  const diagnostic = new URLSearchParams(location.search).get('test') === '1';
  const openDatabase = () => databasePromise ||= new Promise((resolve, reject) => {
    const request = indexedDB.open('TextSpace', 1);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains('workspace')) db.createObjectStore('workspace');
      if (!db.objectStoreNames.contains('history')) db.createObjectStore('history', { keyPath: 'id' });
    };
    request.onsuccess = () => { request.result.onversionchange = () => request.result.close(); resolve(request.result); };
    request.onerror = () => { databasePromise = null; reject(request.error || new Error('Browser storage is unavailable.')); };
    request.onblocked = () => reject(new Error('Close older TextSpace tabs and reload to update local storage.'));
  });
  async function read(store, key) {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(store, 'readonly'); const request = tx.objectStore(store).get(key);
      request.onsuccess = () => resolve(request.result ?? null); request.onerror = () => reject(request.error);
    });
  }
  async function versions() {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
      const request = db.transaction('history', 'readonly').objectStore('history').getAll();
      request.onsuccess = () => resolve(request.result.sort((a, b) => b.savedAt.localeCompare(a.savedAt))); request.onerror = () => reject(request.error);
    });
  }
  async function saveRecovery(title, document) {
    if (typeof document !== 'string' || encoder.encode(document).length > MAX_FILE) throw new Error('Recovery exceeds the browser storage limit.');
    const db = await openDatabase();
    const model = JSON.parse(document); const now = Date.now();
    const createSnapshot = now - lastSnapshot > 120000 || model.id !== latestDocumentId;
    const previous = createSnapshot ? await read('workspace', 'latest') : null;
    const records = createSnapshot ? await versions() : [];
    await new Promise((resolve, reject) => {
      const tx = db.transaction(['workspace', 'history'], 'readwrite');
      const workspace = tx.objectStore('workspace'); const history = tx.objectStore('history');
      if (createSnapshot && previous?.document && previous.document !== document) {
        const stamp = previous.savedAt || new Date().toISOString();
        history.put({ id: stamp + '-' + crypto.randomUUID(), title: previous.title || 'Recovered document', savedAt: stamp, bytes: encoder.encode(previous.document).length, document: previous.document });
      }
      workspace.put({ title, document, savedAt: new Date().toISOString() }, 'latest');
      for (const item of records.slice(11)) history.delete(item.id);
      tx.oncomplete = () => resolve(); tx.onerror = () => reject(tx.error || new Error('Local storage is full. Save a file copy.')); tx.onabort = () => reject(tx.error || new Error('Recovery write was aborted.'));
    });
    if (createSnapshot) { lastSnapshot = now; latestDocumentId = model.id; }
    pendingRecovery = document;
    try { sessionStorage.removeItem('TextSpace-emergency'); } catch { }
  }
  const host = {
    diagnosticsEnabled: () => diagnostic,
    ready() { isReady = true; globalThis.__textSpaceReady = true; document.documentElement.dataset.textspaceReady = 'true'; },
    startupError(message) { globalThis.__textSpaceError = message; console.error(message); },
    publishState(json) { if (diagnostic) globalThis.__textSpaceState = JSON.parse(json); },
    async loadRecovery() {
      const emergency = (() => { try { return sessionStorage.getItem('TextSpace-emergency'); } catch { return null; } })();
      const value = await read('workspace', 'latest');
      if (emergency) {
        try { const candidate = JSON.parse(emergency); const saved = value?.document ? JSON.parse(value.document) : null; if (!saved || new Date(candidate.modified) > new Date(saved.modified)) return emergency; } catch { }
      }
      if (value?.document) { try { latestDocumentId = JSON.parse(value.document).id || ''; } catch { } }
      return value?.document ?? null;
    },
    saveRecovery,
    async listVersions() { return JSON.stringify((await versions()).map(({ id, title, savedAt, bytes }) => ({ id, title, savedAt, bytes }))); },
    async readVersion(id) { return (await read('history', id))?.document ?? null; },
    upload(accept) {
      return new Promise((resolve, reject) => {
        const input = document.createElement('input'); input.type = 'file'; input.accept = accept; input.style.display = 'none'; document.body.append(input);
        let settled = false;
        const finish = value => { if (settled) return; settled = true; input.remove(); resolve(value); };
        input.addEventListener('cancel', () => finish(null), { once: true });
        input.addEventListener('change', async () => {
          try {
            const file = input.files?.[0]; if (!file) return finish(null);
            if (file.size > MAX_FILE) throw new Error('Files must be smaller than 32 MB.');
            const reader = new FileReader();
            reader.onerror = () => { input.remove(); reject(reader.error); };
            reader.onload = () => finish(JSON.stringify({ name: file.name, base64: String(reader.result).split(',')[1] }));
            reader.readAsDataURL(file);
          } catch (error) { input.remove(); reject(error); }
        }, { once: true });
        input.click();
      });
    },
    download(name, base64, contentType) {
      const binary = atob(base64); const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
      const url = URL.createObjectURL(new Blob([bytes], { type: contentType })); const link = document.createElement('a');
      link.href = url; link.download = name; link.rel = 'noopener'; document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 60000);
    },
    async readClipboard() {
      try { return await navigator.clipboard.readText(); }
      catch { throw new Error('Clipboard reading was blocked. Click the document and use Ctrl+V or Command+V to paste.'); }
    },
    async writeClipboard(text) {
      try { await navigator.clipboard.writeText(text); }
      catch {
        const previous = document.activeElement; const field = document.createElement('textarea'); field.value = text; field.style.position = 'fixed'; field.style.left = '-10000px'; document.body.append(field); field.select();
        const copied = document.execCommand('copy'); field.remove(); previous?.focus?.(); if (!copied) throw new Error('Copy was blocked by the browser. Use Ctrl+C or Command+C on selected document text.');
      }
    },
    async printHtml(html) {
      const frame = document.createElement('iframe'); frame.title = 'TextSpace print preview'; frame.style.cssText = 'position:fixed;width:1px;height:1px;bottom:0;right:0;border:0;opacity:0;'; document.body.append(frame);
      await new Promise((resolve, reject) => { frame.onload = resolve; frame.onerror = reject; frame.srcdoc = html; });
      await Promise.all([...frame.contentDocument.images].map(image => image.decode().catch(() => {})));
      const cleanup = () => setTimeout(() => frame.remove(), 1000); frame.contentWindow.addEventListener('afterprint', cleanup, { once: true });
      frame.contentWindow.focus(); frame.contentWindow.print(); setTimeout(() => frame.remove(), 120000);
    },
    openUri(uri) { const address = new URL(uri); if (!['https:', 'http:', 'mailto:'].includes(address.protocol)) throw new Error('Unsupported address scheme.'); window.open(address.href, '_blank', 'noopener,noreferrer'); }
  };
  Object.freeze(host); globalThis.TextSpaceHost = host;
  window.addEventListener('pagehide', () => { if (pendingRecovery && pendingRecovery.length < 2000000) { try { sessionStorage.setItem('TextSpace-emergency', pendingRecovery); } catch { } } });
  document.addEventListener('keydown', event => {
    if (!isReady || !(event.ctrlKey || event.metaKey) || event.altKey) return;
    if (['s', 'o', 'p', 'f', 'h', 'b', 'i', 'u', 'k', 'e', 'l', 'j'].includes(event.key.toLowerCase())) event.preventDefault();
  }, true);
})();
