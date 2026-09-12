"use client";

import { useEffect, useRef, useState } from "react";
import { Html5Qrcode } from "html5-qrcode";

interface QrScannerProps {
  onScan: (code: string) => void;
}

/**
 * Escáner de QR para el panel de cajero.
 * Usa la cámara trasera/ambient cuando está disponible; permite también
 * seleccionar una imagen (foto del QR del cupón) como respaldo.
 */
export default function QrScanner({ onScan }: QrScannerProps) {
  const scannerRef = useRef<Html5Qrcode | null>(null);
  const [scanning, setScanning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    return () => {
      scannerRef.current?.stop().catch(() => {});
      scannerRef.current?.clear();
    };
  }, []);

  async function start() {
    setError(null);
    // Detectar si hay cámara disponible
    const hasCamera = typeof navigator !== "undefined" && !!navigator.mediaDevices?.getUserMedia;
    if (!hasCamera) {
      setError("No hay acceso a cámara en este dispositivo. Usa la opción «Cargar imagen» o escribe el código.");
      setScanning(true);
      return;
    }
    try {
      const scanner = new Html5Qrcode("qr-reader-region");
      scannerRef.current = scanner;
      await scanner.start(
        { facingMode: "environment" },
        { fps: 10, qrbox: { width: 220, height: 220 } },
        (decodedText) => {
          scanner.stop().catch(() => {});
          scanner.clear();
          setScanning(false);
          onScan(decodedText.toUpperCase());
        },
        () => { /* frames intermedios: ignorar */ }
      );
      setScanning(true);
    } catch {
      setScanning(true);
      setError("No se pudo iniciar la cámara. Usa «Cargar imagen» o escribe el código.");
    }
  }

  function stop() {
    scannerRef.current?.stop().catch(() => {});
    scannerRef.current?.clear();
    setScanning(false);
  }

  function onFile(file: File | null) {
    if (!file || !scannerRef.current) return;
    setError(null);
    scannerRef.current
      .scanFile(file, false)
      .then((text) => onScan(text.toUpperCase()))
      .catch(() => setError("No se pudo leer el QR de la imagen."));
  }

  return (
    <div>
      <div id="qr-reader-region" style={{ width: "100%", maxHeight: 280, borderRadius: 12, overflow: "hidden", display: scanning ? "block" : "none", background: "#000" }} />
      <div className="row" style={{ justifyContent: "flex-start", gap: 10, flexWrap: "wrap" }}>
        {!scanning ? (
          <button type="button" className="btn secondary" onClick={start}>📷 Escanear QR</button>
        ) : (
          <button type="button" className="btn ghost" onClick={stop}>Detener cámara</button>
        )}
        {/* Respaldos: imagen / manual */}
        <label className="btn ghost" style={{ width: "auto", minHeight: 40, cursor: "pointer" }}>
          🖼️ Cargar imagen
          <input
            type="file"
            accept="image/*"
            capture="environment"
            style={{ display: "none" }}
            onChange={(e) => onFile(e.target.files?.[0] ?? null)}
          />
        </label>
      </div>
      {error && <p className="muted" style={{ color: "var(--danger)", fontSize: 12, marginTop: 8 }}>{error}</p>}
    </div>
  );
}