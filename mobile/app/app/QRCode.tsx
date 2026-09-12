"use client";

import { useEffect, useState } from "react";
import QR from "qrcode";

/**
 * Renderiza un QR real (SVG) del qrHash del miembro.
 * POR QUÉ qrcode (canvas/svg real) y no un decorativo: en el flujo real este QR se escaneal
 * en caja para identificar al miembro; un hash visual falso rompería el E2E. En esta fase el
 * POS identifica por string qrHash, pero dejamos el QR auténtico para las Fases 5-6 (wallet/scanner).
 */
export default function QRCode({ value }: { value: string }) {
  const [dataUrl, setDataUrl] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    QR.toDataURL(value, { margin: 1, width: 360, errorCorrectionLevel: "M" })
      .then((url) => alive && setDataUrl(url))
      .catch(() => alive && setDataUrl(null));
    return () => {
      alive = false;
    };
  }, [value]);

  if (!dataUrl) return <div className="center muted">Generando…</div>;
  // eslint-disable-next-line @next/next/no-img-element
  return <img src={dataUrl} alt={`Código de fidelidad ${value}`} style={{ maxWidth: 180, width: "100%" }} />;
}