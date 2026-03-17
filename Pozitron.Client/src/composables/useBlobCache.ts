import { ref } from 'vue';
import api from '../api';

const blobCache = new Map<string, string>();
const resolvedUrls = ref<Record<string, string>>({});

export function useBlobCache() {
  const fetchFileAsBlob = async (url: string): Promise<string> => {
    if (blobCache.has(url)) return blobCache.get(url)!;
    try {
      const path = new URL(url).pathname.replace('/api', '');
      const response = await api.get(path, { responseType: 'blob' });
      const blobUrl = URL.createObjectURL(response.data);
      blobCache.set(url, blobUrl);
      return blobUrl;
    } catch {
      return url;
    }
  };

  const resolveUrl = async (url: string) => {
    if (!url || resolvedUrls.value[url]) return;
    if (!url.includes('/api/files/')) {
      resolvedUrls.value[url] = url;
      return;
    }
    resolvedUrls.value[url] = await fetchFileAsBlob(url);
  };

  const getResolvedUrl = (url: string | undefined): string => {
    if (!url) return '';
    if (!url.includes('/api/files/')) return url;
    return resolvedUrls.value[url] || '';
  };

  return { fetchFileAsBlob, resolveUrl, getResolvedUrl, resolvedUrls };
}