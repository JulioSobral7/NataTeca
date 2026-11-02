import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { criarEstante, listarEstantes } from "../api/estantes";
import { ApiError } from "../api/client";
import type { Estante } from "../types/estante";

export default function EstantesPage() {
  const [estantes, setEstantes] = useState<Estante[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [nome, setNome] = useState("");
  const [quantidadePrateleiras, setQuantidadePrateleiras] = useState(4);
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    carregar();
  }, []);

  async function carregar() {
    setCarregando(true);
    try {
      setEstantes(await listarEstantes());
    } catch (e) {
      setErro(e instanceof ApiError ? e.message : "Falha ao carregar estantes.");
    } finally {
      setCarregando(false);
    }
  }

  async function handleCriar(e: React.FormEvent) {
    e.preventDefault();
    if (!nome.trim()) return;

    setSalvando(true);
    setErro(null);
    try {
      await criarEstante({ nome: nome.trim(), quantidadePrateleiras });
      setNome("");
      setQuantidadePrateleiras(4);
      await carregar();
    } catch (e) {
      setErro(e instanceof ApiError ? e.message : "Falha ao criar estante.");
    } finally {
      setSalvando(false);
    }
  }

  return (
    <>
      <div className="top-bar">
        <h1>NataTeca</h1>
      </div>

      {erro && <div className="erro-banner">{erro}</div>}

      <form className="nova-estante-form" onSubmit={handleCriar}>
        <input
          placeholder="Nome da estante (ex: Sala)"
          value={nome}
          onChange={(e) => setNome(e.target.value)}
        />
        <input
          type="number"
          min={1}
          max={50}
          style={{ width: 90 }}
          value={quantidadePrateleiras}
          onChange={(e) => setQuantidadePrateleiras(Number(e.target.value))}
          title="Quantidade de prateleiras"
        />
        <button className="btn btn-primary" disabled={salvando} type="submit">
          Nova estante
        </button>
      </form>

      {carregando ? (
        <p>Carregando...</p>
      ) : estantes.length === 0 ? (
        <p>Nenhuma estante ainda. Crie uma acima.</p>
      ) : (
        <div className="estante-list">
          {estantes.map((estante) => (
            <Link key={estante.id} to={`/estantes/${estante.id}`} className="estante-card">
              <h3>{estante.nome}</h3>
              <p>{estante.quantidadePrateleiras} prateleira(s)</p>
            </Link>
          ))}
        </div>
      )}
    </>
  );
}
