import { apiFetch } from "./client";
import type { Livro, LivroInput, LivroPosicaoInput } from "../types/livro";

export function criarLivro(input: LivroInput) {
  return apiFetch<Livro>("/api/livros", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function criarLivrosEmLote(inputs: LivroInput[]) {
  return apiFetch<Livro[]>("/api/livros/lote", {
    method: "POST",
    body: JSON.stringify(inputs),
  });
}

export function atualizarLivro(id: number, input: LivroInput) {
  return apiFetch<void>(`/api/livros/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function moverLivro(id: number, posicao: LivroPosicaoInput) {
  return apiFetch<void>(`/api/livros/${id}/posicao`, {
    method: "PATCH",
    body: JSON.stringify(posicao),
  });
}

export function excluirLivro(id: number) {
  return apiFetch<void>(`/api/livros/${id}`, { method: "DELETE" });
}
