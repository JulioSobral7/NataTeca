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
4. A API identifica título e autor com um LLM local (Ollama). Por padrão (`VisaoLlm`) o modelo **lê a imagem** de cada
   lombada; na alternativa `Llm`, ele só separa o texto do OCR.
5. A API grava o resultado num arquivo JSON (`API/reconhecimentos/`) e devolve as sugestões para revisão no frontend.
6. Depois da revisão, os livros vão para o banco — pelo frontend ou importando o arquivo direto.

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

## LLM local (identificação de título e autor)

Os modelos rodam no [Ollama](https://ollama.com), em `http://localhost:11434`. `Metadados:Fonte` escolhe o método:

| Fonte | O que faz | Modelo |
|---|---|---|
| `VisaoLlm` (padrão) | lê título e autor direto da imagem de cada lombada | `natateca-vl` (GGUF + mmproj) |
| `Llm` | separa título, autor e ano do texto do OCR | `natateca-llm` (só GGUF) |
| `GoogleBooks` | busca o texto do OCR no Google Books | — |

```bash
# 1. ponha o modelo e o projetor de visão na pasta llm/ (podem ser links)
ln -s /caminho/para/Qwen3.5-9B.Q4_K_M.gguf llm/modelo.gguf
ln -s /caminho/para/Qwen3.5-9B.mmproj-Q8_0.gguf llm/mmproj.gguf

# 2. registre os modelos no Ollama (com o `ollama serve` rodando)
ollama create natateca-vl -f llm/Modelfile.visao   # VisaoLlm
ollama create natateca-llm -f llm/Modelfile        # Llm
```

### Leitura por visão (`VisaoLlm`)

O vision-service devolve a imagem de cada lombada (`POST /reconhecer?incluir_recortes=true`), ampliada e girada.
A API lê cada lombada **duas vezes** — o recorte na vertical, e o recorte girado nos dois sentidos — e só aceita
título/autor quando as duas leituras concordam (similaridade ≥ 0,6). Quando a lombada está ilegível, o modelo
costuma inventar um livro famoso, mas quase nunca o mesmo nas duas leituras; o que é descartado aparece no log.
Textos todos em maiúsculas são convertidos ("THE LORD OF THE RINGS" → "The Lord of the Rings").

Leva cerca de 1,2 s por lombada (30–80 s por prateleira numa RTX 5060 Ti). Não lê o ano.
Ainda passam erros quando o modelo erra igual nas duas leituras — revise antes de salvar.

### Separação do texto do OCR (`Llm`)

Se o modelo importado responder mal (texto sem sentido, "pensando" em voz alta), o template de chat do GGUF
não foi reconhecido. Nesse caso, baixe a versão oficial e aponte a API para ela:
`ollama pull qwen3.5:9b` (já inclui a visão) e `Llm:Modelo` / `Llm:ModeloVisao` = `"qwen3.5:9b"`.

Mais rápido, mas depende do OCR, que lê mal a maioria das lombadas. Para não depender de dados inventados, a API:

- manda as lombadas em lotes pequenos (`Llm:TamanhoLote`, padrão 5): com lotes grandes, um modelo de 9B devolve null para tudo quando a maioria é lixo de OCR, e às vezes entra em repetição;
- pede a saída num JSON Schema fixo, com temperatura 0, sem "thinking" e com limite de tokens;
- descarta título ou autor que não saíram do texto lido (menos de 50% dos bigramas presentes no OCR);
- descarta o ano se ele não estiver escrito na lombada.

O que é descartado aparece no log da API. Configuração (`appsettings.json`):

| Chave | Padrão | |
|---|---|---|
| `Metadados:Fonte` | `VisaoLlm` | `VisaoLlm`, `Llm` ou `GoogleBooks` |
| `Llm:BaseUrl` | `http://localhost:11434/` | endereço do Ollama |
| `Llm:ModeloVisao` | `natateca-vl` | modelo com visão (fonte `VisaoLlm`) |
| `Llm:Modelo` | `natateca-llm` | modelo de texto (fonte `Llm`) |
| `Llm:TimeoutSegundos` | `180` | por chamada; a 1ª carrega o modelo na GPU (~1 min) |
| `Llm:TamanhoLote` | `5` | lombadas por chamada (fonte `Llm`) |

Se o Ollama estiver fora do ar, o reconhecimento continua e as sugestões vêm só com o texto do OCR.

## Arquivo de reconhecimento

Cada foto gera um JSON em `API/reconhecimentos/` (pasta em `Reconhecimento:PastaArquivos`, fora do git), por exemplo
`20261102-213000_estante1_prateleira1.json`. O nome também volta no cabeçalho `X-Arquivo-Reconhecimento`.

```json
{
  "versao": 1,
  "criadoEm": "2026-11-03T00:30:00Z",
  "estanteId": 1,
  "prateleira": 0,
  "foto": "prateleira.jpg",
  "fonte": "VisaoLlm:natateca-vl",
  "livros": [
    { "posicao": 0, "titulo": "Dom Casmurro", "autor": "Machado de Assis", "ano": null,
      "capaUrl": null, "textoOcr": "DOM CASMURR0 MACHAD0 DE ASSIS ATICA", "confianca": 91 }
  ]
}
```

`prateleira` começa em 0 (a "Prateleira 1" da interface); no nome do arquivo ela aparece como na interface.
Dá para corrigir o arquivo à mão e importar:

| Endpoint | |
|---|---|
| `GET /api/reconhecimento/arquivos` | lista os arquivos |
| `GET /api/reconhecimento/arquivos/{nome}` | conteúdo de um arquivo |
| `POST /api/reconhecimento/arquivos/{nome}/importar?posicaoInicial=N` | cadastra os livros no banco |

Na importação, itens sem título são ignorados, autor vazio vira "Desconhecido" e, sem `posicaoInicial`,
os livros entram depois do último livro da prateleira. Se algum item for inválido, nada é gravado e a resposta
traz os erros pela `posicao` do arquivo.

## Google Books (alternativa)

Para voltar ao Google Books, use `Metadados:Fonte = "GoogleBooks"`. Ele também traz a capa.

A busca de metadados usa `GoogleBooks:ApiKey`. Sem chave, a cota é compartilhada e costuma esgotar (erro `429`), deixando título, autor e capa vazios.

**Não coloque a chave em `appsettings.json`** (o repositório é público). Use user-secrets:

```bash
cd API
dotnet user-secrets set "GoogleBooks:ApiKey" "SUA_CHAVE"
```

ou a variável de ambiente `GoogleBooks__ApiKey`.

## Arquivos ignorados

Não vão para o git: `*.pt`, `*.gguf`, `API/reconhecimentos/`, `*.db`, `.env`, `node_modules/`, `dist/`, `bin/`, `obj/`, `.venv/` e a foto de teste `prateleira.png`.
