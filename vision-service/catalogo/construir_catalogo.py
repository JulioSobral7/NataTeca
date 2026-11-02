"""Monta o catálogo local (SQLite) de livros em português a partir dos dumps do Open Library.

Uso:
    python construir_catalogo.py <pasta_dos_dumps> <saida.db>

Duas passagens, para não carregar 15 milhões de autores na memória:
  1) edições: guarda só as edições em português (idioma /languages/por) e as chaves de autor usadas;
  2) autores: lê o dump de autores e guarda só o nome das chaves necessárias.
Depois grava o SQLite, sem duplicatas (mesmo título + autor normalizados).
"""
import gzip
import json
import re
import sqlite3
import sys
import unicodedata
from pathlib import Path

CAPA_URL = "https://covers.openlibrary.org/b/id/{}-M.jpg"


def normalizar(texto: str) -> str:
    sem_acento = "".join(c for c in unicodedata.normalize("NFD", texto) if unicodedata.category(c) != "Mn")
    return re.sub(r"[^a-z0-9]", "", sem_acento.lower())


def linhas(caminho: Path):
    """Cada linha do dump: tipo \\t chave \\t revisão \\t data \\t json."""
    with gzip.open(caminho, "rt", encoding="utf-8", errors="replace") as f:
        for linha in f:
            partes = linha.split("\t", 4)
            if len(partes) == 5:
                yield partes[1], partes[4]


def passagem_edicoes(caminho: Path):
    edicoes = []
    autores_usados: set[str] = set()
    lidas = 0
    for chave, bruto in linhas(caminho):
        lidas += 1
        if lidas % 2_000_000 == 0:
            print(f"  edições lidas: {lidas:,} | em português: {len(edicoes):,}", flush=True)
        # filtro barato antes de decodificar o JSON
        if "/languages/por" not in bruto:
            continue
        try:
            d = json.loads(bruto)
        except json.JSONDecodeError:
            continue
        if not any(l.get("key") == "/languages/por" for l in d.get("languages", [])):
            continue
        titulo = (d.get("title") or "").strip()
        chaves = [a.get("key") for a in d.get("authors", []) if a.get("key")]
        if not titulo or not chaves:
            continue
        if d.get("subtitle"):
            titulo = f"{titulo}: {d['subtitle'].strip()}"
        capas = [c for c in d.get("covers", []) if isinstance(c, int) and c > 0]
        isbns = d.get("isbn_13") or d.get("isbn_10") or []
        edicoes.append((chave, titulo, chaves, capas[0] if capas else None, isbns[0] if isbns else None))
        autores_usados.update(chaves)
    return edicoes, autores_usados


def passagem_autores(caminho: Path, necessarios: set[str]) -> dict[str, str]:
    nomes: dict[str, str] = {}
    for chave, bruto in linhas(caminho):
        if chave not in necessarios:
            continue
        try:
            nome = (json.loads(bruto).get("name") or "").strip()
        except json.JSONDecodeError:
            continue
        if nome:
            nomes[chave] = nome
    return nomes


def main(pasta: Path, saida: Path):
    print("1/3 lendo edições...", flush=True)
    edicoes, necessarios = passagem_edicoes(pasta / "ol_dump_editions.txt.gz")
    print(f"    {len(edicoes):,} edições em português, {len(necessarios):,} autores referenciados", flush=True)

    print("2/3 lendo autores...", flush=True)
    nomes = passagem_autores(pasta / "ol_dump_authors.txt.gz", necessarios)

    print("3/3 gravando SQLite...", flush=True)
    saida.unlink(missing_ok=True)
    db = sqlite3.connect(saida)
    db.execute("""CREATE TABLE livros (
        id INTEGER PRIMARY KEY, titulo TEXT NOT NULL, autor TEXT NOT NULL,
        capa_url TEXT, isbn TEXT, chave_ol TEXT, normalizado TEXT UNIQUE)""")
    vistos = 0
    for chave, titulo, chaves_autor, capa, isbn in edicoes:
        autor = ", ".join(nomes[k] for k in chaves_autor if k in nomes)
        if not autor:
            continue
        norm = normalizar(titulo) + "|" + normalizar(autor)
        db.execute(
            "INSERT OR IGNORE INTO livros (titulo, autor, capa_url, isbn, chave_ol, normalizado) VALUES (?,?,?,?,?,?)",
            (titulo, autor, CAPA_URL.format(capa) if capa else None, isbn, chave, norm),
        )
        vistos += 1
    db.commit()
    total = db.execute("SELECT COUNT(*) FROM livros").fetchone()[0]
    print(f"    {vistos:,} edições com autor -> {total:,} livros únicos", flush=True)
    db.close()


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(Path(sys.argv[1]), Path(sys.argv[2]))
