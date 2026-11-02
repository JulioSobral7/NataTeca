export interface Estante {
  id: number;
  nome: string;
  quantidadePrateleiras: number;
  criadaEm: string;
}

export interface EstanteInput {
  nome: string;
  quantidadePrateleiras: number;
}
