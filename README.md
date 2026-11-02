# NataTeca

Catálogo de livros organizado por estantes e prateleiras. A partir da foto de uma prateleira, o app reconhece as lombadas e sugere os livros.

## Componentes

| Pasta | O que é | Porta padrão |
|---|---|---|
| `API/` | API .NET com SQLite (estantes, livros, reconhecimento) | `https://localhost:5001` |
| `frontend/` | Interface React + Vite | `http://localhost:5173` |
| `vision-service/` | Microsserviço Python: segmentação (YOLO) + OCR (PaddleOCR) | `http://localhost:8000` |

## Como o reconhecimento funciona

1. O frontend envia a foto para `POST /api/reconhecimento/foto` da API.
2. A API repassa a foto ao `vision-service`.
3. O YOLO segmenta cada lombada e o PaddleOCR lê o texto (testando rotações).
4. A API busca título, autor e capa no Google Books a partir do texto lido.

Se o `vision-service` estiver fora do ar, a API responde `503`.

## Como rodar

### 1. vision-service

```bash
cd vision-service
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --port 8000
```

### 2. API

```bash
cd API
dotnet run
```

O banco `natateca.db` (SQLite) é criado/migrado localmente e não vai para o git.
A URL do vision-service pode ser trocada em `VisionService:BaseUrl`.

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
```

A URL da API vem de `VITE_API_BASE_URL` (veja `frontend/.env.development`).

## Modelo YOLO (`*.pt`)

Os arquivos de modelo **não estão no repositório** (`*.pt` está no `.gitignore`).

- **Padrão:** na primeira execução, o `ultralytics` baixa o `yolo11m-seg.pt` (~43 MB) sozinho para a pasta onde o serviço foi iniciado. É preciso internet nesse primeiro uso.
- **Modelo próprio:** depois de um fine-tuning, aponte para o `best.pt` treinado (classe única `spine`):

  ```bash
  SPINE_MODEL=/caminho/para/best.pt uvicorn app.main:app --port 8000
  ```

## OCR

- `OCR_MODELOS=mobile` (padrão): modelos leves do PaddleOCR, mais rápidos.
- `OCR_MODELOS=medium`: modelos maiores, tendem a ler melhor e são mais lentos.

O PaddleOCR roda na CPU (`paddlepaddle`). Em um Mac Apple Silicon não há aceleração por GPU.

## Google Books

A busca de metadados usa `GoogleBooks:ApiKey`. Sem chave, a cota é compartilhada e costuma esgotar (erro `429`), deixando título, autor e capa vazios.

**Não coloque a chave em `appsettings.json`** (o repositório é público). Use user-secrets:

```bash
cd API
dotnet user-secrets set "GoogleBooks:ApiKey" "SUA_CHAVE"
```

ou a variável de ambiente `GoogleBooks__ApiKey`.

## Arquivos ignorados

Não vão para o git: `*.pt`, `*.db`, `.env`, `node_modules/`, `dist/`, `bin/`, `obj/`, `.venv/` e a foto de teste `prateleira.png`.
