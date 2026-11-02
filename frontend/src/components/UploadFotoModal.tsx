import { useState } from "react";
import Modal from "./Modal";
import { ApiError } from "../api/client";
import { reconhecerFoto } from "../api/reconhecimento";
import type { Estante } from "../types/estante";
import type { LivroSugerido } from "../types/livroSugerido";

interface UploadFotoModalProps {
  estante: Estante;
  onClose: () => void;
  onReconhecido: (sugestoes: LivroSugerido[], prateleira: number) => void;
}

export default function UploadFotoModal({ estante, onClose, onReconhecido }: UploadFotoModalProps) {
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [prateleira, setPrateleira] = useState(0);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!arquivo) return;

    setEnviando(true);
    setErro(null);
    try {
      const sugestoes = await reconhecerFoto(arquivo, estante.id, prateleira);
      onReconhecido(sugestoes, prateleira);
    } catch (err) {
      setErro(err instanceof ApiError ? err.message : "Falha ao processar a foto.");
    } finally {
      setEnviando(false);
    }
  }

  const prateleiras = Array.from({ length: estante.quantidadePrateleiras }, (_, i) => i);

  return (
    <Modal onClose={onClose}>
      <h2>Adicionar livros por foto</h2>
      <p className="meta">
        Tire a foto de uma prateleira inteira (uma fileira por vez), de frente e com boa luz. O
        sistema tenta separar as lombadas e ler os títulos automaticamente — você revisa antes de
        salvar.
      </p>

      {erro && <div className="erro-banner">{erro}</div>}

      <form onSubmit={handleSubmit}>
        <div className="form-field">
          <label>Prateleira da foto</label>
          <select value={prateleira} onChange={(e) => setPrateleira(Number(e.target.value))}>
            {prateleiras.map((p) => (
              <option key={p} value={p}>
                Prateleira {p + 1}
              </option>
            ))}
          </select>
        </div>

        <div className="form-field">
          <label>Foto</label>
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            onChange={(e) => setArquivo(e.target.files?.[0] ?? null)}
            required
          />
        </div>

        <div className="modal-actions">
          <button className="btn" type="button" onClick={onClose} disabled={enviando}>
            Cancelar
          </button>
          <button className="btn btn-primary" type="submit" disabled={enviando || !arquivo}>
            {enviando ? "Analisando foto..." : "Reconhecer livros"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
