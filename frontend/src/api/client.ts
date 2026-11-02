export class ApiError extends Error {
  status: number;
  body: unknown;

  constructor(status: number, message: string, body?: unknown) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

async function parseErrorBody(response: Response): Promise<{ message: string; body: unknown }> {
  const text = await response.text();
  if (!text) return { message: response.statusText, body: undefined };

  try {
    const json = JSON.parse(text);
    if (typeof json === "string") return { message: json, body: json };
    if (json && typeof json === "object") {
      if (typeof (json as { title?: unknown }).title === "string") {
        return { message: (json as { title: string }).title, body: json };
      }
      if ("erros" in json) {
        return { message: "Alguns itens são inválidos. Veja os detalhes por linha.", body: json };
      }
    }
    return { message: text, body: json };
  } catch {
    return { message: text, body: text };
  }
}

export async function apiFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
  const isFormData = options.body instanceof FormData;

  const response = await fetch(`${baseUrl}${path}`, {
    ...options,
    headers: {
      ...(options.body && !isFormData ? { "Content-Type": "application/json" } : {}),
      ...options.headers,
    },
  });

  if (!response.ok) {
    const { message, body } = await parseErrorBody(response);
    throw new ApiError(response.status, message, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  return text ? (JSON.parse(text) as T) : (undefined as T);
}
