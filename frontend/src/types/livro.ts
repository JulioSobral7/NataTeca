export type StatusLeitura = "QueroLer" | "Lendo" | "Lido";

export interface Livro {
  id: number;
  titulo: string;
  autor: string;
  ano: number | null;
  genero: string | null;
  capaUrl: string | null;
  status: StatusLeitura;
  adicionadoEm: string;
  estanteId: number;
  prateleira: number;
  posicao: number;
}

export interface LivroInput {
  titulo: string;
  autor: string;
  ano: number | null;
  genero: string | null;
  capaUrl: string | null;
  status: StatusLeitura;
  estanteId: number;
  prateleira: number;
  posicao: number;
}

export interface LivroPosicaoInput {
  prateleira: number;
  posicao: number;
}
