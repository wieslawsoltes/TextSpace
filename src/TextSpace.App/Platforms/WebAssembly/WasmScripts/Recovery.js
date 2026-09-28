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
    if (!Array.isArray(value) || value.length > MAX_COUNT || value.some(x => !validId(x.id) || !Number.isSafeInteger(x.bytes) || x.bytes < 0 || typeof x.savedAt !== 'string'))
      throw new Error('Protected recovery index is invalid; no original was changed.');
    return value;
  }
  globalThis.TextSpaceRecovery = Object.freeze({
    async protect(original) {
      if (typeof original !== 'string' || original.length > MAX_ORIGINAL) throw new Error('Original recovery exceeds 32 MB.');
      const bytes = encoder.encode(original);
      if (bytes.length > MAX_ORIGINAL) throw new Error('Original recovery exceeds 32 MB.');
      const id = await digest(bytes), db = await database();
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
              raw.onsuccess = () => { if (raw.result !== original) fail(new Error('Protected original checksum conflict.')); else result = existing; };
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
    },
    async list() { return JSON.stringify(metadata((await get(INDEX)) ?? []).sort((a, b) => b.savedAt.localeCompare(a.savedAt))); },
    async read(id) {
      if (!validId(id)) throw new Error('Invalid protected recovery identifier.');
      const original = await get(prefix + id);
      if (original === undefined) return null;
      if (typeof original !== 'string' || original.length > MAX_ORIGINAL) throw new Error('Invalid protected original.');
      const bytes = encoder.encode(original);
      if (bytes.length > MAX_ORIGINAL || await digest(bytes) !== id) throw new Error('Protected original checksum verification failed.');
      return original;
    },
    complete() { delete globalThis.__textSpaceError; delete globalThis.__textSpaceRecoveryState; },
    publishState(json) { if (new URLSearchParams(location.search).get('test') === '1') globalThis.__textSpaceRecoveryState = JSON.parse(json); }
  });
})();
