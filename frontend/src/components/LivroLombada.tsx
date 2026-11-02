import { resolveCapaUrl } from "../api/capas";
import type { Livro } from "../types/livro";

const PALETA = ["#5a4fcf", "#c2543e", "#2f7a5e", "#a5722f", "#7a3b8a", "#3b6e8a", "#8a3b57"];

function corPorTitulo(titulo: string): string {
  let hash = 0;
  for (let i = 0; i < titulo.length; i++) {
    hash = titulo.charCodeAt(i) + ((hash << 5) - hash);
  }
  return PALETA[Math.abs(hash) % PALETA.length];
}

interface LivroLombadaProps {
  livro: Livro;
  onClick: () => void;
  onExcluir: () => void;
  onDragStart: () => void;
}

export default function LivroLombada({ livro, onClick, onExcluir, onDragStart }: LivroLombadaProps) {
  const capa = resolveCapaUrl(livro.capaUrl);

  return (
    <div
      className="lombada"
      draggable
      onDragStart={onDragStart}
      onClick={onClick}
      style={{
        background: capa ? undefined : corPorTitulo(livro.titulo),
        backgroundImage: capa ? `url(${capa})` : undefined,
      }}
      title={`${livro.titulo} — ${livro.autor}`}
    >
      {!capa && <span className="titulo">{livro.titulo}</span>}
      <button
        className="excluir-livro"
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          onExcluir();
        }}
        title="Excluir livro"
      >
        ×
      </button>
    </div>
  );
}
