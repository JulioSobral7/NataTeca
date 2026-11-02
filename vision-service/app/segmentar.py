"""Segmentação de lombadas com YOLO-seg.

Por padrão usa pesos COCO pré-treinados filtrando a classe "book" (baseline, sem fine-tuning).
Depois do fine-tuning, aponte SPINE_MODEL para o best.pt treinado (classe única "spine").
"""
import os
from dataclasses import dataclass

import cv2
import numpy as np
from ultralytics import YOLO

COCO_BOOK_CLASS = 73
MODELO = os.environ.get("SPINE_MODEL", "yolo11m-seg.pt")
USA_COCO = "SPINE_MODEL" not in os.environ

_modelo: YOLO | None = None


@dataclass
class Lombada:
    imagem: np.ndarray  # BGR, lado maior na vertical
    x: int
    y: int
    largura: int
    altura: int
    conf: float = 0.0


def _carregar() -> YOLO:
    global _modelo
    if _modelo is None:
        _modelo = YOLO(MODELO)
    return _modelo


def _recortar(imagem: np.ndarray, contorno: np.ndarray) -> np.ndarray:
    """Recorta o retângulo mínimo rotacionado do contorno, endireitado."""
    (cx, cy), (w, h), ang = cv2.minAreaRect(contorno.astype(np.float32))
    if w > h:  # garante lado maior na vertical
        w, h, ang = h, w, ang - 90
    matriz = cv2.getRotationMatrix2D((cx, cy), ang, 1.0)
    girada = cv2.warpAffine(imagem, matriz, (imagem.shape[1], imagem.shape[0]), flags=cv2.INTER_CUBIC)
    x0, y0 = int(max(cx - w / 2, 0)), int(max(cy - h / 2, 0))
    return girada[y0:int(cy + h / 2), x0:int(cx + w / 2)]


def _ordenar_por_prateleira(itens: list[Lombada]) -> list[Lombada]:
    """Agrupa por prateleira (centro-y) e ordena cada uma da esquerda para a direita."""
    if not itens:
        return itens
    altura_mediana = float(np.median([i.altura for i in itens]))
    itens = sorted(itens, key=lambda i: i.y + i.altura / 2)
    linhas: list[list[Lombada]] = [[itens[0]]]
    for item in itens[1:]:
        centro_linha = np.mean([i.y + i.altura / 2 for i in linhas[-1]])
        if (item.y + item.altura / 2) - centro_linha > 0.6 * altura_mediana:
            linhas.append([item])
        else:
            linhas[-1].append(item)
    return [i for linha in linhas for i in sorted(linha, key=lambda i: i.x)]


def _remover_duplicatas(itens: list[Lombada], limite: float = 0.6) -> list[Lombada]:
    """Descarta caixas que se sobrepõem demais (interseção / área da menor), mantendo a de maior confiança."""
    mantidos: list[Lombada] = []
    for item in sorted(itens, key=lambda i: -i.conf):
        duplicada = False
        for outro in mantidos:
            ix = max(0, min(item.x + item.largura, outro.x + outro.largura) - max(item.x, outro.x))
            iy = max(0, min(item.y + item.altura, outro.y + outro.altura) - max(item.y, outro.y))
            menor = min(item.largura * item.altura, outro.largura * outro.altura)
            if menor > 0 and (ix * iy) / menor > limite:
                duplicada = True
                break
        if not duplicada:
            mantidos.append(item)
    return mantidos


def segmentar(imagem: np.ndarray, conf: float = 0.15) -> list[Lombada]:
    modelo = _carregar()
    resultado = modelo.predict(
        imagem,
        imgsz=1280,
        conf=conf,
        iou=0.45,
        classes=[COCO_BOOK_CLASS] if USA_COCO else None,
        retina_masks=True,
        verbose=False,
    )[0]

    lombadas: list[Lombada] = []
    if resultado.masks is None:
        return lombadas

    confiancas = resultado.boxes.conf.tolist()
    for contorno, c in zip(resultado.masks.xy, confiancas):
        if len(contorno) < 3:
            continue
        x, y, w, h = cv2.boundingRect(contorno.astype(np.int32))
        recorte = _recortar(imagem, contorno)
        if recorte.size == 0 or min(recorte.shape[:2]) < 10:
            continue
        lombadas.append(Lombada(recorte, x, y, w, h, c))

    return _ordenar_por_prateleira(_remover_duplicatas(lombadas))
