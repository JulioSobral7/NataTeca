import { apiFetch } from "./client";
import type { Estante, EstanteInput } from "../types/estante";
import type { Livro } from "../types/livro";

export function listarEstantes() {
  return apiFetch<Estante[]>("/api/estantes");
}

export function obterEstante(id: number) {
  return apiFetch<Estante>(`/api/estantes/${id}`);
}

export function criarEstante(input: EstanteInput) {
  return apiFetch<Estante>("/api/estantes", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function atualizarEstante(id: number, input: EstanteInput) {
  return apiFetch<void>(`/api/estantes/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function excluirEstante(id: number) {
  return apiFetch<void>(`/api/estantes/${id}`, { method: "DELETE" });
}

export function listarLivrosDaEstante(estanteId: number) {
  return apiFetch<Livro[]>(`/api/estantes/${estanteId}/livros`);
}
