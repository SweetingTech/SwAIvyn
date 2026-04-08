import { useRef, useEffect, Suspense } from 'react';
import { Canvas, useFrame, useThree } from '@react-three/fiber';
import { OrbitControls, Environment, useGLTF, Html } from '@react-three/drei';
import * as THREE from 'three';

// ─── Procedural avatar (used when no model URL is provided) ──────────────────

interface ProceduralAvatarProps {
  isListening: boolean;
  isSpeaking: boolean;
  isProcessing: boolean;
}

function ProceduralAvatar({ isListening, isSpeaking, isProcessing }: ProceduralAvatarProps) {
  const groupRef = useRef<THREE.Group>(null);
  const headRef = useRef<THREE.Mesh>(null);
  const bodyRef = useRef<THREE.Mesh>(null);
  const coreRef = useRef<THREE.Mesh>(null);
  const eyeLeftRef = useRef<THREE.Mesh>(null);
  const eyeRightRef = useRef<THREE.Mesh>(null);

  const bodyColor = isListening
    ? '#ef4444'
    : isSpeaking
    ? '#06b6d4'
    : isProcessing
    ? '#eab308'
    : '#8b5cf6';

  const emissiveIntensity = isListening ? 0.4 : isSpeaking ? 0.5 : isProcessing ? 0.3 : 0.1;

  useFrame((state) => {
    const t = state.clock.getElapsedTime();

    if (groupRef.current) {
      // Gentle idle float
      groupRef.current.position.y = Math.sin(t * 0.8) * 0.05;
      if (!isSpeaking && !isListening) {
        groupRef.current.rotation.y = Math.sin(t * 0.3) * 0.1;
      }
    }

    if (headRef.current) {
      if (isSpeaking) {
        // Lip-sync approximation: subtle head bob + scale
        headRef.current.scale.y = 1 + Math.sin(t * 8) * 0.04;
        headRef.current.scale.x = 1 - Math.sin(t * 8) * 0.02;
      } else {
        headRef.current.scale.setScalar(1 + Math.sin(t * 1.5) * 0.01);
      }
    }

    if (coreRef.current) {
      const pulse = isListening
        ? Math.sin(t * 4) * 0.15 + 0.85
        : isSpeaking
        ? Math.sin(t * 10) * 0.2 + 0.8
        : isProcessing
        ? Math.sin(t * 3) * 0.1 + 0.9
        : 1;
      coreRef.current.scale.setScalar(pulse);
    }

    // Eye blink
    if (eyeLeftRef.current && eyeRightRef.current) {
      const blink = Math.floor(t * 3) % 5 === 0 && (t * 3) % 1 < 0.15 ? 0.1 : 1;
      eyeLeftRef.current.scale.y = blink;
      eyeRightRef.current.scale.y = blink;
    }
  });

  return (
    <group ref={groupRef} position={[0, 0, 0]}>
      {/* Glowing core sphere */}
      <mesh ref={coreRef} position={[0, 0.1, 0]}>
        <sphereGeometry args={[0.55, 32, 32]} />
        <meshStandardMaterial
          color="#000000"
          emissive={bodyColor}
          emissiveIntensity={emissiveIntensity * 0.5}
          transparent
          opacity={0.25}
        />
      </mesh>

      {/* Body */}
      <mesh ref={bodyRef} position={[0, -0.45, 0]}>
        <capsuleGeometry args={[0.25, 0.55, 8, 16]} />
        <meshStandardMaterial
          color={bodyColor}
          emissive={bodyColor}
          emissiveIntensity={emissiveIntensity * 0.3}
          roughness={0.4}
          metalness={0.3}
        />
      </mesh>

      {/* Left arm */}
      <mesh position={[-0.4, -0.35, 0]} rotation={[0, 0, 0.4]}>
        <capsuleGeometry args={[0.08, 0.38, 6, 12]} />
        <meshStandardMaterial color={bodyColor} roughness={0.4} metalness={0.2} />
      </mesh>

      {/* Right arm */}
      <mesh position={[0.4, -0.35, 0]} rotation={[0, 0, -0.4]}>
        <capsuleGeometry args={[0.08, 0.38, 6, 12]} />
        <meshStandardMaterial color={bodyColor} roughness={0.4} metalness={0.2} />
      </mesh>

      {/* Head */}
      <mesh ref={headRef} position={[0, 0.2, 0]}>
        <sphereGeometry args={[0.28, 32, 32]} />
        <meshStandardMaterial
          color={bodyColor}
          emissive={bodyColor}
          emissiveIntensity={emissiveIntensity}
          roughness={0.3}
          metalness={0.4}
        />
      </mesh>

      {/* Eyes */}
      <mesh ref={eyeLeftRef} position={[-0.1, 0.24, 0.24]}>
        <sphereGeometry args={[0.05, 12, 12]} />
        <meshStandardMaterial color="#ffffff" emissive="#ffffff" emissiveIntensity={0.8} />
      </mesh>
      <mesh ref={eyeRightRef} position={[0.1, 0.24, 0.24]}>
        <sphereGeometry args={[0.05, 12, 12]} />
        <meshStandardMaterial color="#ffffff" emissive="#ffffff" emissiveIntensity={0.8} />
      </mesh>

      {/* Pupil left */}
      <mesh position={[-0.1, 0.24, 0.285]}>
        <sphereGeometry args={[0.025, 8, 8]} />
        <meshStandardMaterial color="#111111" />
      </mesh>
      {/* Pupil right */}
      <mesh position={[0.1, 0.24, 0.285]}>
        <sphereGeometry args={[0.025, 8, 8]} />
        <meshStandardMaterial color="#111111" />
      </mesh>

      {/* Point light: follows avatar color */}
      <pointLight color={bodyColor} intensity={1.5} distance={3} position={[0, 0.5, 0.5]} />
    </group>
  );
}

// ─── Loaded glTF / VRM model ──────────────────────────────────────────────────

interface ModelAvatarProps {
  url: string;
  isListening: boolean;
  isSpeaking: boolean;
  isProcessing: boolean;
}

function ModelAvatar({ url, isSpeaking }: ModelAvatarProps) {
  const { scene } = useGLTF(url);
  const groupRef = useRef<THREE.Group>(null);

  useFrame((state) => {
    const t = state.clock.getElapsedTime();
    if (groupRef.current) {
      groupRef.current.position.y = Math.sin(t * 0.8) * 0.03;
      if (isSpeaking) {
        groupRef.current.scale.y = 1 + Math.sin(t * 8) * 0.01;
      }
    }
  });

  return (
    <group ref={groupRef} scale={1.2}>
      <primitive object={scene} />
    </group>
  );
}

// ─── Floor / Room ─────────────────────────────────────────────────────────────

interface RoomItemDef {
  id: string;
  label: string;
  position: [number, number, number];
  color: string;
  scale: [number, number, number];
}

const ITEM_GEOMETRIES: Record<string, JSX.Element> = {
  plant: <cylinderGeometry args={[0.12, 0.15, 0.3, 8]} />,
  lamp:  <cylinderGeometry args={[0.05, 0.05, 0.8, 8]} />,
  book:  <boxGeometry args={[0.15, 0.2, 0.08]} />,
  rug:   <boxGeometry args={[1.2, 0.02, 0.8]} />,
  chair: <boxGeometry args={[0.4, 0.4, 0.4]} />,
};

interface RoomProps {
  activeItems: string[];
}

function Room({ activeItems }: RoomProps) {
  const ITEMS: RoomItemDef[] = [
    { id: 'plant',  label: 'Plant',  position: [-1.8, -0.85, -1.2], color: '#22c55e', scale: [1,1,1] },
    { id: 'lamp',   label: 'Lamp',   position: [1.8, -0.55, -1.2],  color: '#fbbf24', scale: [1,1,1] },
    { id: 'book',   label: 'Book',   position: [1.5, -0.88, 1.0],   color: '#60a5fa', scale: [1,1,1] },
    { id: 'rug',    label: 'Rug',    position: [0, -0.99, 0],       color: '#a78bfa', scale: [1,1,1] },
    { id: 'chair',  label: 'Chair',  position: [-1.5, -0.79, 0.8],  color: '#f97316', scale: [1,1,1] },
  ];

  return (
    <>
      {/* Floor */}
      <mesh position={[0, -1.0, 0]} receiveShadow>
        <boxGeometry args={[6, 0.1, 5]} />
        <meshStandardMaterial color="#1e293b" roughness={0.8} />
      </mesh>

      {/* Back wall */}
      <mesh position={[0, 0.5, -2.55]} receiveShadow>
        <boxGeometry args={[6, 3, 0.1]} />
        <meshStandardMaterial color="#0f172a" roughness={0.9} />
      </mesh>

      {/* Left wall */}
      <mesh position={[-3.05, 0.5, 0]} receiveShadow>
        <boxGeometry args={[0.1, 3, 5]} />
        <meshStandardMaterial color="#0f172a" roughness={0.9} />
      </mesh>

      {/* Right wall */}
      <mesh position={[3.05, 0.5, 0]} receiveShadow>
        <boxGeometry args={[0.1, 3, 5]} />
        <meshStandardMaterial color="#0f172a" roughness={0.9} />
      </mesh>

      {/* Room items */}
      {ITEMS.filter((item) => activeItems.includes(item.id)).map((item) => (
        <mesh key={item.id} position={item.position} scale={item.scale} castShadow>
          {ITEM_GEOMETRIES[item.id] ?? <boxGeometry args={[0.3, 0.3, 0.3]} />}
          <meshStandardMaterial color={item.color} roughness={0.6} />
        </mesh>
      ))}
    </>
  );
}

// ─── Camera rig ──────────────────────────────────────────────────────────────

function CameraRig({ isSpeaking }: { isSpeaking: boolean }) {
  const { camera } = useThree();

  useFrame((state) => {
    const t = state.clock.getElapsedTime();
    // Subtle camera sway
    camera.position.x += (Math.sin(t * 0.2) * 0.02 - camera.position.x) * 0.02;
    camera.position.y += (0.4 + (isSpeaking ? 0.03 * Math.sin(t * 6) : 0) - camera.position.y) * 0.04;
    camera.lookAt(0, 0, 0);
  });

  return null;
}

// ─── Loading fallback ─────────────────────────────────────────────────────────

function ModelLoader() {
  return (
    <Html center>
      <div className="text-cyan-400 text-sm animate-pulse">Loading model…</div>
    </Html>
  );
}

// ─── Public component ─────────────────────────────────────────────────────────

export interface Avatar3DSceneProps {
  isListening: boolean;
  isSpeaking: boolean;
  isProcessing: boolean;
  modelUrl?: string;          // Optional user-supplied glTF/VRM URL
  activeRoomItems?: string[]; // IDs of room items to show
  characterName?: string;
}

const Avatar3DScene = ({
  isListening,
  isSpeaking,
  isProcessing,
  modelUrl,
  activeRoomItems = [],
  characterName = 'AI Assistant',
}: Avatar3DSceneProps) => {
  return (
    <div className="w-full h-full min-h-[480px] relative rounded-xl overflow-hidden">
      <Canvas
        camera={{ position: [0, 0.4, 3], fov: 50 }}
        shadows
        gl={{ antialias: true }}
      >
        {/* Lighting */}
        <ambientLight intensity={0.4} />
        <directionalLight
          position={[2, 4, 3]}
          intensity={1.2}
          castShadow
          shadow-mapSize-width={1024}
          shadow-mapSize-height={1024}
        />
        <pointLight position={[-2, 2, -2]} intensity={0.5} color="#6366f1" />

        {/* Camera rig */}
        <CameraRig isSpeaking={isSpeaking} />

        {/* Environment preset for reflections */}
        <Environment preset="night" />

        {/* Room */}
        <Room activeItems={activeRoomItems} />

        {/* Avatar */}
        {modelUrl ? (
          <Suspense fallback={<ModelLoader />}>
            <ModelAvatar
              url={modelUrl}
              isListening={isListening}
              isSpeaking={isSpeaking}
              isProcessing={isProcessing}
            />
          </Suspense>
        ) : (
          <ProceduralAvatar
            isListening={isListening}
            isSpeaking={isSpeaking}
            isProcessing={isProcessing}
          />
        )}

        {/* Character name overlay */}
        <Html position={[0, 0.85, 0]} center>
          <div className="text-white text-sm font-light tracking-widest select-none pointer-events-none whitespace-nowrap">
            {characterName}
          </div>
        </Html>

        <OrbitControls
          enablePan={false}
          enableZoom={false}
          minPolarAngle={Math.PI / 4}
          maxPolarAngle={Math.PI / 2}
          minAzimuthAngle={-Math.PI / 4}
          maxAzimuthAngle={Math.PI / 4}
        />
      </Canvas>
    </div>
  );
};

export default Avatar3DScene;
