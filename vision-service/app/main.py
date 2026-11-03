import io
import logging
import os
import time

import cv2
import numpy as np
from fastapi import FastAPI, File, HTTPException, Query, UploadFile
from PIL import Image, ImageOps

from . import ocr, recortes, segmentar

log = logging.getLogger("uvicorn.error")

# Recortes quase uniformes (sem contraste) não têm texto: pula o OCR.
CONTRASTE_MINIMO = 12.0

app = FastAPI(title="NataTeca Vision")

DEBUG_DIR = os.environ.get("DEBUG_CROPS_DIR")  # se definido, salva os recortes para inspeção


@app.get("/saude")
def saude():
    return {"status": "ok"}


@app.post("/reconhecer")
async def reconhecer(
    foto: UploadFile = File(...),
    incluir_recortes: bool = Query(False, description="Devolve as imagens das lombadas para leitura por um modelo de visão."),
):
    dados = await foto.read()
    if not dados:
        raise HTTPException(400, "Arquivo vazio.")
    try:
        pil = ImageOps.exif_transpose(Image.open(io.BytesIO(dados))).convert("RGB")
    except Exception:
        raise HTTPException(400, "Imagem inválida.")
    imagem = cv2.cvtColor(np.array(pil), cv2.COLOR_RGB2BGR)

    t0 = time.perf_counter()
    lombadas = segmentar.segmentar(imagem)
    t_seg = time.perf_counter() - t0

    if DEBUG_DIR:
        os.makedirs(DEBUG_DIR, exist_ok=True)

    saida = []
    pulados = 0
    t1 = time.perf_counter()
    for i, lombada in enumerate(lombadas):
        imagens = None
        if float(cv2.cvtColor(lombada.imagem, cv2.COLOR_BGR2GRAY).std()) < CONTRASTE_MINIMO:
            texto, confianca = "", 0
            pulados += 1
        else:
            texto, confianca = ocr.reconhecer_melhor_rotacao(lombada.imagem)
            if incluir_recortes:
                imagens = recortes.preparar(lombada.imagem)
        if DEBUG_DIR:
            cv2.imwrite(os.path.join(DEBUG_DIR, f"{i:02d}.png"), lombada.imagem)
        saida.append({
            "posicao": i,
            "caixa": {"x": lombada.x, "y": lombada.y, "largura": lombada.largura, "altura": lombada.altura},
            "texto": texto,
            "confianca": confianca,
            "recortes": imagens,  # null se não pedido ou se o recorte não tem contraste (sem texto)
        })
    log.info("segmentacao=%.1fs ocr=%.1fs recortes=%d pulados=%d", t_seg, time.perf_counter() - t1, len(lombadas), pulados)
    return saida
