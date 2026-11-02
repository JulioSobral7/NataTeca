import { apiFetch } from "./client";
import type { LivroSugerido } from "../types/livroSugerido";

export function reconhecerFoto(foto: File, estanteId: number, prateleira: number) {
  const formData = new FormData();
  formData.append("foto", foto);
  formData.append("estanteId", String(estanteId));
  formData.append("prateleira", String(prateleira));

  return apiFetch<LivroSugerido[]>("/api/reconhecimento/foto", {
    method: "POST",
    body: formData,
  });
}
