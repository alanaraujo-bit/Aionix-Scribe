"use client";

import { Canvas, useFrame } from "@react-three/fiber";
import { Float, MeshDistortMaterial, Environment } from "@react-three/drei";
import { useEffect, useMemo, useRef, useState } from "react";
import * as THREE from "three";

/*
  Objeto 3D do topo: um orbe que pulsa como voz e reage ao cursor.

  Três decisões de desempenho, porque "com elementos 3D" e "performático" brigam entre si:
   1. Este arquivo é carregado por next/dynamic com ssr:false — nada de three.js no HTML inicial.
   2. dpr limitado a 1.5: em telas de alta densidade, renderizar em 3x custa caro e a diferença
      visual num objeto desfocado e orgânico como este é imperceptível.
   3. frameloop="demand" NÃO é usado aqui de propósito (o orbe anima continuamente), mas o Canvas
      pausa sozinho quando sai da viewport via a checagem de visibilidade abaixo.
*/

function Orb({ pointer }: { pointer: React.RefObject<{ x: number; y: number }> }) {
  const mesh = useRef<THREE.Mesh>(null);

  useFrame((state) => {
    if (!mesh.current) return;
    const t = state.clock.elapsedTime;

    // Rotação lenta e contínua dá vida sem chamar atenção para si mesma.
    mesh.current.rotation.y = t * 0.18;
    mesh.current.rotation.z = Math.sin(t * 0.2) * 0.12;

    // Segue o cursor com amortecimento — movimento direto pareceria mecânico.
    const target = pointer.current ?? { x: 0, y: 0 };
    mesh.current.position.x += (target.x * 0.35 - mesh.current.position.x) * 0.04;
    mesh.current.position.y += (target.y * 0.25 - mesh.current.position.y) * 0.04;

    // Respiração: escala oscilando de leve, como amplitude de voz.
    const breath = 1 + Math.sin(t * 0.9) * 0.025;
    mesh.current.scale.setScalar(breath);
  });

  return (
    <Float speed={1.1} rotationIntensity={0.25} floatIntensity={0.5}>
      <mesh ref={mesh}>
        <icosahedronGeometry args={[1.35, 64]} />
        <MeshDistortMaterial
          color="#e8763f"
          emissive="#8a3a12"
          emissiveIntensity={0.35}
          roughness={0.18}
          metalness={0.85}
          distort={0.38}
          speed={1.4}
        />
      </mesh>
    </Float>
  );
}

/// Anéis finos girando em volta: dão profundidade e leem como ondas de áudio concêntricas.
function Rings() {
  const group = useRef<THREE.Group>(null);
  useFrame((state) => {
    if (!group.current) return;
    group.current.rotation.x = state.clock.elapsedTime * 0.12;
    group.current.rotation.y = state.clock.elapsedTime * 0.08;
  });

  const radii = useMemo(() => [1.9, 2.3, 2.75], []);
  return (
    <group ref={group}>
      {radii.map((r, i) => (
        <mesh key={r} rotation={[Math.PI / 2 + i * 0.4, i * 0.3, 0]}>
          <torusGeometry args={[r, 0.006, 12, 128]} />
          <meshBasicMaterial color="#e8763f" transparent opacity={0.22 - i * 0.05} />
        </mesh>
      ))}
    </group>
  );
}

export default function VoiceOrb() {
  const pointer = useRef({ x: 0, y: 0 });
  const [visible, setVisible] = useState(true);
  const wrapper = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onMove = (e: PointerEvent) => {
      pointer.current = {
        x: (e.clientX / window.innerWidth) * 2 - 1,
        y: -((e.clientY / window.innerHeight) * 2 - 1),
      };
    };
    window.addEventListener("pointermove", onMove, { passive: true });
    return () => window.removeEventListener("pointermove", onMove);
  }, []);

  // Fora da viewport, o Canvas para de renderizar: um objeto 3D animando fora da tela é
  // bateria e CPU jogados fora enquanto a pessoa lê o resto da página.
  useEffect(() => {
    const el = wrapper.current;
    if (!el) return;
    const observer = new IntersectionObserver(
      ([entry]) => setVisible(entry.isIntersecting),
      { rootMargin: "120px" },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  return (
    <div ref={wrapper} className="h-full w-full">
      <Canvas
        camera={{ position: [0, 0, 6], fov: 40 }}
        dpr={[1, 1.5]}
        frameloop={visible ? "always" : "never"}
        gl={{ antialias: true, alpha: true, powerPreference: "high-performance" }}
      >
        <ambientLight intensity={0.5} />
        <directionalLight position={[4, 5, 4]} intensity={2.2} color="#ffd9c2" />
        <directionalLight position={[-5, -2, -3]} intensity={1.1} color="#7c5cff" />
        <Orb pointer={pointer} />
        <Rings />
        <Environment preset="city" />
      </Canvas>
    </div>
  );
}
