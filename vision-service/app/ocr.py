"""OCR neural (PaddleOCR) com teste de rotações, equivalente ao ReconhecerMelhorRotacaoAsync do .NET."""
import os

import cv2
import numpy as np
from paddleocr import PaddleOCR

_ocr: PaddleOCR | None = None

# Ordem de tentativa: lombadas brasileiras têm texto de cima para baixo (gira 90° anti-horário para ficar legível).
# As demais só são testadas se a primeira não for confiável.
ROTACOES = [cv2.ROTATE_90_COUNTERCLOCKWISE, cv2.ROTATE_90_CLOCKWISE, None]
CONFIANCA_SUFICIENTE = 80
MIN_CARACTERES_SUFICIENTE = 4

# Modelos leves (mais rápidos). Use OCR_MODELOS=medium para voltar aos modelos maiores.
USA_MOBILE = os.environ.get("OCR_MODELOS", "mobile") == "mobile"


def _carregar() -> PaddleOCR:
    global _ocr
    if _ocr is None:
        modelos = (
            {"text_detection_model_name": "PP-OCRv5_mobile_det",
             "text_recognition_model_name": "latin_PP-OCRv5_mobile_rec"}
            if USA_MOBILE else {"lang": "pt"}
        )
        _ocr = PaddleOCR(
            **modelos,
            use_doc_orientation_classify=False,
            use_doc_unwarping=False,
            use_textline_orientation=True,
        )
    return _ocr


def _ler(imagem: np.ndarray) -> tuple[str, int]:
    resultado = _carregar().predict(imagem)
    textos: list[str] = []
    scores: list[float] = []
    for r in resultado:
        textos += list(r["rec_texts"])
        scores += [float(s) for s in r["rec_scores"]]
    pares = [(t.strip(), s) for t, s in zip(textos, scores) if t.strip()]
    if not pares:
        return "", 0
    texto = " ".join(t for t, _ in pares)
    confianca = int(round(100 * sum(s for _, s in pares) / len(pares)))
    return texto, confianca


def reconhecer_melhor_rotacao(recorte: np.ndarray) -> tuple[str, int]:
    melhor = ("", 0)
    melhor_pontos = -1.0
    for rot in ROTACOES:
        img = recorte if rot is None else cv2.rotate(recorte, rot)
        texto, conf = _ler(img)
        # pondera pela quantidade de texto para não preferir um único caractere "confiante"
        pontos = conf * min(len(texto), 20)
        if pontos > melhor_pontos:
            melhor, melhor_pontos = (texto, conf), pontos
        # parada antecipada: leitura confiável e com texto suficiente dispensa as outras rotações
        if conf >= CONFIANCA_SUFICIENTE and len(texto) >= MIN_CARACTERES_SUFICIENTE:
            break
    return melhor
