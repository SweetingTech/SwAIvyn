import fetchWithTimeout from './fetchWithTimeout';

/**
 * Fetch with retries, exponential backoff, and per‑attempt timeout.
 * - Retries on network errors and typical transient HTTP statuses (500/502/503/504)
 * - Exponential backoff capped at 4s
 * - Default: 10 retries, 400ms initial backoff, 5s attempt timeout
 * - Optional onAttempt callback for visibility of attempt progress
 */
export default async function fetchWithRetry(
  url: string,
  init: RequestInit = {},
  retries = 10,
  backoffMs = 400,
  timeoutMs = 5000,
  onAttempt?: (attempt: number, total: number) => void,
  _attempt = 1,
  _total = retries
): Promise<Response> {
  onAttempt?.(_attempt, _total);
  try {
    console.debug(`[fetchWithRetry] ${url} attempt ${_attempt}/${_total}`);
    const res = await fetchWithTimeout(url, timeoutMs, init);
    if (!res.ok && [500, 502, 503, 504].includes(res.status)) {
      throw new Error(`Retryable HTTP status: ${res.status}`);
    }
    return res;
  } catch (err) {
    if (retries <= 0) throw err;
    await new Promise((r) => setTimeout(r, backoffMs));
    return fetchWithRetry(
      url,
      init,
      retries - 1,
      Math.min(backoffMs * 2, 4000),
      timeoutMs,
      onAttempt,
      _attempt + 1,
      _total
    );
  }
}

