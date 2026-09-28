/* Protected originals live outside rolling history. No document is uploaded. */
(() => {
  'use strict';
  const encoder = new TextEncoder();
  const INDEX = 'protected-originals-index';
  const prefix = 'protected-original/';
  const MAX_ORIGINAL = 32 * 1024 * 1024, MAX_TOTAL = 64 * 1024 * 1024, MAX_COUNT = 16;
  const validId = id => typeof id === 'string' && /^[a-f0-9]{64}$/.test(id);
  let opening;
  function database() {
    if (opening) return opening;
    opening = new Promise((resolve, reject) => {
      const request = indexedDB.open('TextSpace', 1);
      request.onupgradeneeded = () => {
        const db = request.result;
        if (!db.objectStoreNames.contains('workspace')) db.createObjectStore('workspace');
        if (!db.objectStoreNames.contains('history')) db.createObjectStore('history', { keyPath: 'id' });
      };
      request.onsuccess = () => {
        const db = request.result;
        db.onversionchange = () => { db.close(); opening = null; };
        resolve(db);
      };
      request.onerror = () => { opening = null; reject(request.error); };
      request.onblocked = () => reject(new Error('Protected recovery storage is blocked by another tab.'));
    });
    return opening;
  }
  async function digest(bytes) {
    return [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))].map(x => x.toString(16).padStart(2, '0')).join('');
  }
  async function get(key) {
    const db = await database();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('workspace', 'readonly'); const request = tx.objectStore('workspace').get(key);
      let value;
      request.onsuccess = () => { value = request.result; };
      tx.oncomplete = () => resolve(value);
      tx.onabort = tx.onerror = () => reject(tx.error || new Error('Could not read protected recovery.'));
    });
  }
  function metadata(value) {
    if (!Array.isArray(value) || value.length > MAX_COUNT || value.some(x => !x || !validId(x.id) || !Number.isSafeInteger(x.bytes) || x.bytes < 0 || x.bytes > MAX_ORIGINAL || typeof x.savedAt !== 'string'))
      throw new Error('Protected recovery index is invalid; no original was changed.');
    return value;
  }
  function originalBytes(original) {
    if (typeof original !== 'string' || original.length > MAX_ORIGINAL) throw new Error('Original recovery exceeds 32 MB.');
    const bytes = encoder.encode(original);
    if (bytes.length > MAX_ORIGINAL) throw new Error('Original recovery exceeds 32 MB.');
    return bytes;
  }
  async function protectOriginal(original) {
    const bytes = originalBytes(original), id = await digest(bytes), db = await database();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('workspace', 'readwrite'), store = tx.objectStore('workspace');
      let result, failure;
      const fail = error => { failure = error; tx.abort(); };
      const request = store.get(INDEX);
      request.onsuccess = () => {
        try {
          const entries = metadata(request.result ?? []), existing = entries.find(x => x.id === id);
          if (existing) {
            const raw = store.get(prefix + id);
            raw.onsuccess = () => { if (raw.result !== original || existing.bytes !== bytes.length) fail(new Error('Protected original checksum conflict.')); else result = existing; };
            return;
          }
          if (entries.length >= MAX_COUNT || entries.reduce((sum, x) => sum + x.bytes, 0) + bytes.length > MAX_TOTAL)
            throw new Error('Protected recovery storage is full. Download the original; no existing backup has been removed.');
          result = { id, savedAt: new Date().toISOString(), bytes: bytes.length };
          store.add(original, prefix + id);
          store.put([...entries, result], INDEX);
        } catch (error) { fail(error); }
      };
      tx.oncomplete = () => resolve(JSON.stringify(result));
      tx.onabort = tx.onerror = () => reject(failure || tx.error || new Error('Original protection failed; active recovery is unchanged.'));
    });
  }
  async function readOriginal(id) {
    if (!validId(id)) throw new Error('Invalid protected recovery identifier.');
    const original = await get(prefix + id);
    if (original === undefined) return null;
    if (await digest(originalBytes(original)) !== id) throw new Error('Protected original checksum verification failed.');
    return original;
  }
  globalThis.TextSpaceRecovery = Object.freeze({
    protect: protectOriginal,
    read: readOriginal,
    // Envelope framing keeps a leading U+FEFF inside data, not at the beginning
    // of a native interop string. Verify both byte count and checksum before I/O.
    async protectPayload(envelope) {
      if (typeof envelope !== 'string' || envelope.length > MAX_ORIGINAL * 6 + 512)
        throw new Error('Protected recovery transport exceeds its size limit.');
      const payload = JSON.parse(envelope);
      if (!payload || !validId(payload.sha256) || !Number.isSafeInteger(payload.utf8Bytes))
        throw new Error('Invalid protected recovery transport.');
      const bytes = originalBytes(payload.original);
      if (bytes.length !== payload.utf8Bytes || await digest(bytes) !== payload.sha256)
        throw new Error('Protected recovery transport checksum verification failed; no original was changed.');
      return protectOriginal(payload.original);
    },
    async readPayload(id) {
      const original = await readOriginal(id);
      return original === null ? null : JSON.stringify({ original, sha256: id, utf8Bytes: originalBytes(original).length });
    },
    async list() { return JSON.stringify(metadata((await get(INDEX)) ?? []).sort((a, b) => b.savedAt.localeCompare(a.savedAt))); },
    complete() { delete globalThis.__textSpaceError; delete globalThis.__textSpaceRecoveryState; },
    publishState(json) { if (new URLSearchParams(location.search).get('test') === '1') globalThis.__textSpaceRecoveryState = JSON.parse(json); }
  });
})();
