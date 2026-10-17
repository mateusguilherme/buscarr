import { vi } from 'vitest';

export function mockFetch(...responses: { status: number; body?: unknown }[]) {
  const fetchMock = vi.fn<typeof fetch>();

  for (const { status, body } of responses) {
    fetchMock.mockResolvedValueOnce(
      new Response(body === undefined ? null : JSON.stringify(body), { status }),
    );
  }

  vi.stubGlobal('fetch', fetchMock);

  return fetchMock;
}
