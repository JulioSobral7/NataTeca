import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import GradeEstante from "../components/GradeEstante";
import LivroFormModal from "../components/LivroFormModal";
import UploadFotoModal from "../components/UploadFotoModal";
import RevisaoSugestoesModal from "../components/RevisaoSugestoesModal";
import { ApiError } from "../api/client";
import { excluirEstante, listarLivrosDaEstante, obterEstante } from "../api/estantes";
import { excluirLivro, moverLivro } from "../api/livros";
import type { Estante } from "../types/estante";
import type { Livro } from "../types/livro";
import type { LivroSugerido } from "../types/livroSugerido";

export default function EstanteViewPage() {
  const { id } = useParams<{ id: string }>();
  const estanteId = Number(id);
  const navigate = useNavigate();

  const [estante, setEstante] = useState<Estante | null>(null);
  const [livros, setLivros] = useState<Livro[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [livroEmEdicao, setLivroEmEdicao] = useState<Livro | null>(null);
  const [mostrarNovoLivro, setMostrarNovoLivro] = useState(false);
  const [mostrarUploadFoto, setMostrarUploadFoto] = useState(false);
  const [revisao, setRevisao] = useState<{ prateleira: number; sugestoes: LivroSugerido[] } | null>(
    null,
  );

  useEffect(() => {
    carregar();
  }, [estanteId]);

  async function carregar() {
    setCarregando(true);
    try {
      const [estanteCarregada, livrosCarregados] = await Promise.all([
        obterEstante(estanteId),
        listarLivrosDaEstante(estanteId),
      ]);
      setEstante(estanteCarregada);
      setLivros(livrosCarregados);
    } catch (e) {
      setErro(e instanceof ApiError ? e.message : "Falha ao carregar a estante.");
    } finally {
      setCarregando(false);
    }
  }

  function proximaPosicaoLivre(prateleira: number) {
    const daLinha = livros.filter((l) => l.prateleira === prateleira);
    return daLinha.length > 0 ? Math.max(...daLinha.map((l) => l.posicao)) + 1 : 0;
  }

  async function handleMoverLivro(livroId: number, prateleira: number, posicao: number) {
    setErro(null);
    try {
      await moverLivro(livroId, { prateleira, posicao });
      await carregar();
    } catch (e) {
      setErro(e instanceof ApiError ? e.message : "Falha ao mover livro.");
      await carregar();
    }
  }

  async function handleExcluirLivro(livro: Livro) {
    if (!confirm(`Excluir "${livro.titulo}"?`)) return;
    setErro(null);
    try {
      await excluirLivro(livro.id);
      await carregar();
    } catch (e) {
      setErro(e instanceof ApiError ? e.message : "Falha ao excluir livro.");
    }
  }

  async function handleExcluirEstante() {
    if (!estante) return;
    if (!confirm(`Excluir a estante "${estante.nome}"?`)) return;
    setErro(null);
    try {
      await excluirEstante(estante.id);
      navigate("/");
    } catch (e) {
      setErro(e instanceof ApiError ? e.message : "Falha ao excluir estante.");
    }
  }

  if (carregando) return <p>Carregando...</p>;
  if (!estante) return <p>Estante não encontrada.</p>;

  return (
    <>
      <div className="top-bar">
        <div>
          <Link to="/">&larr; Estantes</Link>
          <h1>{estante.nome}</h1>
        </div>
        <button className="btn btn-danger" onClick={handleExcluirEstante}>
          Excluir estante
        </button>
      </div>

      {erro && <div className="erro-banner">{erro}</div>}

      <div className="estante-toolbar">
        <button className="btn btn-primary" onClick={() => setMostrarNovoLivro(true)}>
          Adicionar livro
        </button>
        <button className="btn" onClick={() => setMostrarUploadFoto(true)}>
          Adicionar por foto
        </button>
      </div>

      <GradeEstante
        estante={estante}
        livros={livros}
        onClicarLivro={setLivroEmEdicao}
        onExcluirLivro={handleExcluirLivro}
        onMoverLivro={handleMoverLivro}
      />

      {mostrarNovoLivro && (
        <LivroFormModal
          estante={estante}
          prateleiraInicial={0}
          posicaoInicial={proximaPosicaoLivre(0)}
          onClose={() => setMostrarNovoLivro(false)}
          onSalvo={carregar}
        />
      )}

      {livroEmEdicao && (
        <LivroFormModal
          estante={estante}
          livroExistente={livroEmEdicao}
          prateleiraInicial={livroEmEdicao.prateleira}
          posicaoInicial={livroEmEdicao.posicao}
          onClose={() => setLivroEmEdicao(null)}
          onSalvo={carregar}
        />
      )}

      {mostrarUploadFoto && (
        <UploadFotoModal
          estante={estante}
          onClose={() => setMostrarUploadFoto(false)}
          onReconhecido={(sugestoes, prateleira) => {
            setMostrarUploadFoto(false);
            setRevisao({ prateleira, sugestoes });
          }}
        />
      )}

      {revisao && (
        <RevisaoSugestoesModal
          estante={estante}
          prateleira={revisao.prateleira}
          posicaoInicial={proximaPosicaoLivre(revisao.prateleira)}
          sugestoes={revisao.sugestoes}
          onClose={() => setRevisao(null)}
          onConfirmado={carregar}
        />
      )}
    </>
  );
}
