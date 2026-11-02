import { useState } from "react";
import Modal from "./Modal";
import { ApiError } from "../api/client";
import { criarLivro, atualizarLivro } from "../api/livros";
import { enviarCapa, resolveCapaUrl } from "../api/capas";
import type { Estante } from "../types/estante";
import type { Livro, StatusLeitura } from "../types/livro";

interface LivroFormModalProps {
  estante: Estante;
  livroExistente?: Livro;
  prateleiraInicial: number;
  posicaoInicial: number;
  onClose: () => void;
  onSalvo: () => void;
}

const OPCOES_STATUS: { valor: StatusLeitura; rotulo: string }[] = [
  { valor: "QueroLer", rotulo: "Quero ler" },
  { valor: "Lendo", rotulo: "Lendo" },
  { valor: "Lido", rotulo: "Lido" },
];

export default function LivroFormModal({
  estante,
  livroExistente,
  prateleiraInicial,
  posicaoInicial,
  onClose,
  onSalvo,
}: LivroFormModalProps) {
  const [titulo, setTitulo] = useState(livroExistente?.titulo ?? "");
  const [autor, setAutor] = useState(livroExistente?.autor ?? "");
  const [ano, setAno] = useState<string>(livroExistente?.ano?.toString() ?? "");
  const [genero, setGenero] = useState(livroExistente?.genero ?? "");
  const [status, setStatus] = useState<StatusLeitura>(livroExistente?.status ?? "QueroLer");
  const [capaUrl, setCapaUrl] = useState<string | null>(livroExistente?.capaUrl ?? null);
  const [prateleira, setPrateleira] = useState(livroExistente?.prateleira ?? prateleiraInicial);
  const [posicao, setPosicao] = useState(livroExistente?.posicao ?? posicaoInicial);

  const [enviandoCapa, setEnviandoCapa] = useState(false);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function handleEscolherCapa(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = e.target.files?.[0];
    if (!arquivo) return;

    setEnviandoCapa(true);
    setErro(null);
    try {
      const resultado = await enviarCapa(arquivo);
      setCapaUrl(resultado.url);
    } catch (err) {
      setErro(err instanceof ApiError ? err.message : "Falha ao enviar capa.");
    } finally {
      setEnviandoCapa(false);
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!titulo.trim() || !autor.trim()) return;

    setSalvando(true);
    setErro(null);
    try {
      const input = {
        titulo: titulo.trim(),
        autor: autor.trim(),
        ano: ano ? Number(ano) : null,
        genero: genero.trim() || null,
        capaUrl,
        status,
        estanteId: estante.id,
        prateleira,
        posicao,
      };

      if (livroExistente) {
        await atualizarLivro(livroExistente.id, input);
      } else {
        await criarLivro(input);
      }

      onSalvo();
      onClose();
    } catch (err) {
      setErro(err instanceof ApiError ? err.message : "Falha ao salvar livro.");
    } finally {
      setSalvando(false);
    }
  }

  const capaPreview = resolveCapaUrl(capaUrl);

  return (
    <Modal onClose={onClose}>
      <h2>{livroExistente ? "Editar livro" : "Novo livro"}</h2>

      {erro && <div className="erro-banner">{erro}</div>}

      <form onSubmit={handleSubmit}>
        <div className="form-field">
          <label>Título</label>
          <input value={titulo} onChange={(e) => setTitulo(e.target.value)} required />
        </div>

        <div className="form-field">
          <label>Autor</label>
          <input value={autor} onChange={(e) => setAutor(e.target.value)} required />
        </div>

        <div className="form-row">
          <div className="form-field">
            <label>Ano</label>
            <input type="number" value={ano} onChange={(e) => setAno(e.target.value)} />
          </div>
          <div className="form-field">
            <label>Gênero</label>
            <input value={genero} onChange={(e) => setGenero(e.target.value)} />
          </div>
        </div>

        <div className="form-field">
          <label>Status</label>
          <select value={status} onChange={(e) => setStatus(e.target.value as StatusLeitura)}>
            {OPCOES_STATUS.map((o) => (
              <option key={o.valor} value={o.valor}>
                {o.rotulo}
              </option>
            ))}
          </select>
        </div>

        <div className="form-row">
          <div className="form-field">
            <label>Prateleira</label>
            <input
              type="number"
              min={0}
              max={estante.quantidadePrateleiras - 1}
              value={prateleira}
              onChange={(e) => setPrateleira(Number(e.target.value))}
            />
          </div>
          <div className="form-field">
            <label>Posição</label>
            <input
              type="number"
              min={0}
              value={posicao}
              onChange={(e) => setPosicao(Number(e.target.value))}
            />
          </div>
        </div>

        <div className="form-field">
          <label>Capa</label>
          <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
            {capaPreview && <img className="capa-preview" src={capaPreview} alt="" />}
            <input type="file" accept="image/png,image/jpeg,image/webp" onChange={handleEscolherCapa} />
          </div>
          {enviandoCapa && <span className="meta">Enviando...</span>}
        </div>

        <div className="modal-actions">
          <button className="btn" type="button" onClick={onClose}>
            Cancelar
          </button>
          <button className="btn btn-primary" type="submit" disabled={salvando || enviandoCapa}>
            Salvar
          </button>
        </div>
      </form>
    </Modal>
  );
}
