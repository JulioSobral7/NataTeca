import { useState } from "react";
import LivroLombada from "./LivroLombada";
import type { Estante } from "../types/estante";
import type { Livro } from "../types/livro";

interface GradeEstanteProps {
  estante: Estante;
  livros: Livro[];
  onClicarLivro: (livro: Livro) => void;
  onExcluirLivro: (livro: Livro) => void;
  onMoverLivro: (livroId: number, prateleira: number, posicao: number) => void;
}

export default function GradeEstante({
  estante,
  livros,
  onClicarLivro,
  onExcluirLivro,
  onMoverLivro,
}: GradeEstanteProps) {
  const [arrastandoId, setArrastandoId] = useState<number | null>(null);
  const [prateleiraAlvo, setPrateleiraAlvo] = useState<number | null>(null);

  const prateleiras = Array.from({ length: estante.quantidadePrateleiras }, (_, i) => i);

  function livrosDaPrateleira(indice: number) {
    return livros
      .filter((l) => l.prateleira === indice)
      .sort((a, b) => a.posicao - b.posicao);
  }

  function handleDrop(indicePrateleira: number) {
    if (arrastandoId === null) return;

    const livrosAlvo = livrosDaPrateleira(indicePrateleira).filter((l) => l.id !== arrastandoId);
    const novaPosicao = livrosAlvo.length > 0 ? Math.max(...livrosAlvo.map((l) => l.posicao)) + 1 : 0;

    onMoverLivro(arrastandoId, indicePrateleira, novaPosicao);
    setArrastandoId(null);
    setPrateleiraAlvo(null);
  }

  return (
    <div className="grade-estante">
      {prateleiras.map((indice) => {
        const livrosDaLinha = livrosDaPrateleira(indice);
        return (
          <div
            key={indice}
            className={`prateleira-row${prateleiraAlvo === indice ? " arrastando-sobre" : ""}`}
            onDragOver={(e) => {
              e.preventDefault();
              setPrateleiraAlvo(indice);
            }}
            onDragLeave={() => setPrateleiraAlvo((atual) => (atual === indice ? null : atual))}
            onDrop={() => handleDrop(indice)}
          >
            <span className="prateleira-label">Prateleira {indice + 1}</span>
            {livrosDaLinha.length === 0 && (
              <span className="prateleira-vazia">Vazia — arraste ou adicione um livro</span>
            )}
            {livrosDaLinha.map((livro) => (
              <LivroLombada
                key={livro.id}
                livro={livro}
                onClick={() => onClicarLivro(livro)}
                onExcluir={() => onExcluirLivro(livro)}
                onDragStart={() => setArrastandoId(livro.id)}
              />
            ))}
          </div>
        );
      })}
    </div>
  );
}
