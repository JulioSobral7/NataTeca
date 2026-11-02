export interface LivroSugerido {
  posicao: number;
  tituloSugerido: string | null;
  autorSugerido: string | null;
  capaUrlSugerida: string | null;
  textoOcrBruto: string;
  confianca: number;
}
