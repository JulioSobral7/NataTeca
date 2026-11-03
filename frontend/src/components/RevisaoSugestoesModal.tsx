import { useState } from "react";
import Modal from "./Modal";
import { ApiError } from "../api/client";
import { criarLivrosEmLote } from "../api/livros";
import type { Estante } from "../types/estante";
import type { LivroSugerido } from "../types/livroSugerido";
import type { LivroInput } from "../types/livro";

interface ItemRevisao {
  titulo: string;
  autor: string;
  ano: string;
  capaUrl: string | null;
  textoOcrBruto: string;
  confianca: number;
}

interface RevisaoSugestoesModalProps {
  estante: Estante;
  prateleira: number;
  posicaoInicial: number;
  sugestoes: LivroSugerido[];
  onClose: () => void;
  onConfirmado: () => void;
}

interface ErrosLote {
  erros: Record<string, string>;
}

function ehErrosLote(body: unknown): body is ErrosLote {
  return typeof body === "object" && body !== null && "erros" in body;
}

export default function RevisaoSugestoesModal({
  estante,
  prateleira,
  posicaoInicial,
  sugestoes,
  onClose,
  onConfirmado,
}: RevisaoSugestoesModalProps) {
  const [itens, setItens] = useState<ItemRevisao[]>(
    sugestoes.map((s) => ({
      titulo: s.tituloSugerido ?? "",
      autor: s.autorSugerido ?? "",
      ano: s.anoSugerido?.toString() ?? "",
      capaUrl: s.capaUrlSugerida,
      textoOcrBruto: s.textoOcrBruto,
      confianca: s.confianca,
    })),
  );
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const [errosPorLinha, setErrosPorLinha] = useState<Record<number, string>>({});
  const [salvando, setSalvando] = useState(false);

  function atualizarItem(indice: number, campo: "titulo" | "autor" | "ano", valor: string) {
    setItens((atual) => atual.map((item, i) => (i === indice ? { ...item, [campo]: valor } : item)));
  }

  function removerItem(indice: number) {
    setItens((atual) => atual.filter((_, i) => i !== indice));
  }

  async function handleConfirmar() {
    if (itens.length === 0) {
      onClose();
      return;
    }

    setSalvando(true);
    setErroGeral(null);
    setErrosPorLinha({});

    const inputs: LivroInput[] = itens.map((item, indice) => ({
      titulo: item.titulo.trim() || `Livro não identificado ${indice + 1}`,
      autor: item.autor.trim() || "Desconhecido",
      ano: Number.parseInt(item.ano, 10) || null,
      genero: null,
      capaUrl: item.capaUrl,
      status: "QueroLer",
      estanteId: estante.id,
      prateleira,
      posicao: posicaoInicial + indice,
    }));

    try {
      await criarLivrosEmLote(inputs);
      onConfirmado();
      onClose();
    } catch (err) {
      if (err instanceof ApiError && ehErrosLote(err.body)) {
        const mapeado: Record<number, string> = {};
        for (const [indice, mensagem] of Object.entries(err.body.erros)) {
          mapeado[Number(indice)] = mensagem;
        }
        setErrosPorLinha(mapeado);
        setErroGeral("Corrija os itens destacados e tente novamente.");
      } else {
        setErroGeral(err instanceof ApiError ? err.message : "Falha ao salvar os livros.");
      }
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal onClose={onClose}>
      <h2>Revisar livros identificados</h2>
      <p className="meta">
        Prateleira {prateleira + 1} — corrija título/autor/ano quando o OCR errar, e remova linhas que
        não correspondem a um livro real (cortes errados da segmentação).
      </p>

      {erroGeral && <div className="erro-banner">{erroGeral}</div>}

      {itens.length === 0 && <p>Nenhum item restante. Feche esta janela.</p>}

      {itens.map((item, indice) => (
        <div className="sugestao-linha" key={indice}>
          {item.capaUrl && <img className="capa-preview" src={item.capaUrl} alt="" />}
          <div className="campos">
            <input
              placeholder="Título"
              value={item.titulo}
              onChange={(e) => atualizarItem(indice, "titulo", e.target.value)}
            />
            <input
              placeholder="Autor"
              value={item.autor}
              onChange={(e) => atualizarItem(indice, "autor", e.target.value)}
            />
            <input
              placeholder="Ano"
              inputMode="numeric"
              value={item.ano}
              onChange={(e) => atualizarItem(indice, "ano", e.target.value.replace(/\D/g, "").slice(0, 4))}
            />
            <span className={`meta${item.confianca < 50 ? " confianca-baixa" : ""}`}>
              OCR: "{item.textoOcrBruto || "(vazio)"}" · confiança {item.confianca}%
            </span>
            {errosPorLinha[indice] && <span className="erro-linha">{errosPorLinha[indice]}</span>}
          </div>
          <button className="btn btn-danger" type="button" onClick={() => removerItem(indice)}>
            Remover
          </button>
        </div>
      ))}

      <div className="modal-actions">
        <button className="btn" type="button" onClick={onClose} disabled={salvando}>
          Cancelar
        </button>
        <button
          className="btn btn-primary"
          type="button"
          onClick={handleConfirmar}
          disabled={salvando || itens.length === 0}
        >
          Confirmar e salvar {itens.length > 0 ? `(${itens.length})` : ""}
        </button>
      </div>
    </Modal>
  );
}
