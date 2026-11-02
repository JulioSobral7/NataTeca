import { apiFetch } from "./client";

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

export function enviarCapa(arquivo: File) {
  const formData = new FormData();
  formData.append("arquivo", arquivo);
  return apiFetch<{ url: string }>("/api/capas", {
    method: "POST",
    body: formData,
  });
}

export function resolveCapaUrl(url?: string | null): string | undefined {
  if (!url) return undefined;
  if (url.startsWith("http://") || url.startsWith("https://")) return url;
  return `${baseUrl}${url}`;
}
