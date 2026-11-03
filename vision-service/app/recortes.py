"""Prepara os recortes das lombadas para a leitura por um modelo de visão (feita pela API)."""
import base64

import cv2
import numpy as np

# Os recortes costumam ter 20-50 px de largura; ampliar ajuda o encoder de visão do modelo.
LADO_MAIOR = 768
QUALIDADE_JPEG = 90


def _ampliar(img: np.ndarray) -> np.ndarray:
    h, w = img.shape[:2]
    fator = LADO_MAIOR / max(h, w)
    if fator <= 1:
        return img
    return cv2.resize(img, (max(1, round(w * fator)), max(1, round(h * fator))), interpolation=cv2.INTER_LANCZOS4)


def _jpeg_base64(img: np.ndarray) -> str:
    ok, buf = cv2.imencode(".jpg", img, [cv2.IMWRITE_JPEG_QUALITY, QUALIDADE_JPEG])
    if not ok:
        raise ValueError("Falha ao codificar o recorte em JPEG.")
    return base64.b64encode(buf.tobytes()).decode("ascii")


def preparar(recorte: np.ndarray) -> dict:
    """Recorte ampliado na vertical e girado nos dois sentidos (lombadas em PT e EN têm texto em sentidos opostos)."""
    return {
        "vertical": _jpeg_base64(_ampliar(recorte)),
        "girados": [
            _jpeg_base64(_ampliar(cv2.rotate(recorte, cv2.ROTATE_90_COUNTERCLOCKWISE))),
            _jpeg_base64(_ampliar(cv2.rotate(recorte, cv2.ROTATE_90_CLOCKWISE))),
        ],
    }
