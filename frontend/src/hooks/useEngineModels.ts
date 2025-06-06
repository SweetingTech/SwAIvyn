import { useQuery } from '@tanstack/react-query';
import fetchWithTimeout from '../utils/fetchWithTimeout';

export default function useEngineModels(engine: string) {
  return useQuery({
    queryKey: ['models', engine],
    queryFn: async () => {
      const res = await fetchWithTimeout(`/api/llm/${engine}/models`);
      if (!res.ok) throw new Error(`${engine} unavailable`);
      return res.json();
    },
    staleTime: 15 * 60 * 1000,
    retry: 1,
  });
}
