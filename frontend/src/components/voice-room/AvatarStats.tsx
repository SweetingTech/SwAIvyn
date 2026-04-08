import { useState, useEffect, useCallback } from 'react';
import { motion } from 'framer-motion';
import { Heart, Zap, Star } from 'lucide-react';

export interface AvatarStats {
  energy: number;       // 0-100
  mood: number;         // 0-100
  relationship: number; // 0-100
}

const DEFAULT_STATS: AvatarStats = { energy: 80, mood: 70, relationship: 50 };
const STORAGE_KEY = 'swaivyn_avatar_stats';

/** Clamp a value to [0, 100], returning the default if not a valid number */
function clampStat(val: unknown, def: number): number {
  const n = Number(val);
  if (!isFinite(n)) return def;
  return Math.max(0, Math.min(100, n));
}

export function loadStats(): AvatarStats {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as Record<string, unknown>;
      return {
        energy:       clampStat(parsed.energy,       DEFAULT_STATS.energy),
        mood:         clampStat(parsed.mood,          DEFAULT_STATS.mood),
        relationship: clampStat(parsed.relationship,  DEFAULT_STATS.relationship),
      };
    }
  } catch {
    // ignore
  }
  return { ...DEFAULT_STATS };
}

export function saveStats(stats: AvatarStats): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(stats));
  } catch {
    // ignore
  }
}

/** Call this after a successful AI interaction to update stats */
export function applyInteractionBoost(stats: AvatarStats): AvatarStats {
  return {
    energy:       Math.max(0, stats.energy - 5),
    mood:         Math.min(100, stats.mood + 3),
    relationship: Math.min(100, stats.relationship + 2),
  };
}

/** Natural decay – call periodically (e.g. once per minute) */
export function applyNaturalDecay(stats: AvatarStats): AvatarStats {
  return {
    energy:       Math.max(0, stats.energy - 1),
    mood:         Math.max(0, stats.mood - 0.5),
    relationship: stats.relationship, // relationship doesn't decay passively
  };
}

// ─── StatBar sub-component ────────────────────────────────────────────────────

interface StatBarProps {
  label: string;
  value: number;
  icon: React.ReactNode;
  color: string;
  bgColor: string;
}

function StatBar({ label, value, icon, color, bgColor }: StatBarProps) {
  const pct = Math.round(value);
  return (
    <div className="flex items-center gap-2">
      <div className={`${color} flex-shrink-0`}>{icon}</div>
      <div className="flex-grow">
        <div className="flex justify-between text-xs mb-0.5">
          <span className="text-gray-400 font-medium">{label}</span>
          <span className="text-gray-300">{pct}%</span>
        </div>
        <div className="h-1.5 rounded-full bg-gray-700 overflow-hidden">
          <motion.div
            className={`h-full rounded-full ${bgColor}`}
            initial={{ width: 0 }}
            animate={{ width: `${pct}%` }}
            transition={{ duration: 0.6, ease: 'easeOut' }}
          />
        </div>
      </div>
    </div>
  );
}

// ─── Mood label helper ────────────────────────────────────────────────────────

function getMoodLabel(mood: number): string {
  if (mood >= 80) return '😄 Elated';
  if (mood >= 60) return '😊 Happy';
  if (mood >= 40) return '😐 Neutral';
  if (mood >= 20) return '😕 Gloomy';
  return '😟 Sad';
}

function getEnergyLabel(energy: number): string {
  if (energy >= 80) return '⚡ Full';
  if (energy >= 50) return '🔋 OK';
  if (energy >= 20) return '🪫 Low';
  return '💀 Depleted';
}

// ─── Public component ─────────────────────────────────────────────────────────

interface AvatarStatsProps {
  stats: AvatarStats;
  onStatsChange?: (stats: AvatarStats) => void;
}

const AvatarStatsPanel = ({ stats, onStatsChange }: AvatarStatsProps) => {
  const [isExpanded, setIsExpanded] = useState(false);

  const handleRest = useCallback(() => {
    const next = { ...stats, energy: Math.min(100, stats.energy + 20) };
    saveStats(next);
    onStatsChange?.(next);
  }, [stats, onStatsChange]);

  return (
    <div className="bg-gray-900/80 backdrop-blur-sm border border-gray-700/50 rounded-xl overflow-hidden shadow-lg">
      {/* Header – always visible */}
      <button
        className="w-full flex items-center justify-between px-4 py-2 hover:bg-gray-800/50 transition-colors"
        onClick={() => setIsExpanded((v) => !v)}
        aria-expanded={isExpanded}
        aria-label="Toggle AI stats panel"
      >
        <span className="text-xs font-semibold text-gray-300 tracking-widest uppercase">AI Stats</span>
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-500">{getMoodLabel(stats.mood)}</span>
          <span className={`text-xs transition-transform duration-200 ${isExpanded ? 'rotate-180' : ''}`}>▾</span>
        </div>
      </button>

      {/* Expanded panel */}
      <motion.div
        initial={false}
        animate={{ height: isExpanded ? 'auto' : 0, opacity: isExpanded ? 1 : 0 }}
        transition={{ duration: 0.25, ease: 'easeInOut' }}
        className="overflow-hidden"
      >
        <div className="px-4 pb-3 pt-1 space-y-2">
          <StatBar
            label="Energy"
            value={stats.energy}
            icon={<Zap size={12} />}
            color="text-yellow-400"
            bgColor="bg-yellow-400"
          />
          <StatBar
            label="Mood"
            value={stats.mood}
            icon={<Heart size={12} />}
            color="text-pink-400"
            bgColor="bg-pink-400"
          />
          <StatBar
            label="Bond"
            value={stats.relationship}
            icon={<Star size={12} />}
            color="text-cyan-400"
            bgColor="bg-cyan-400"
          />

          <div className="flex justify-between items-center pt-1 text-xs text-gray-500">
            <span>{getEnergyLabel(stats.energy)}</span>
            <button
              onClick={handleRest}
              className="px-2 py-0.5 rounded bg-gray-700 hover:bg-gray-600 text-gray-300 text-xs transition-colors"
              title="Let the AI rest to recover energy"
            >
              Rest (+20 ⚡)
            </button>
          </div>
        </div>
      </motion.div>
    </div>
  );
};

// ─── Hook for managing stats lifecycle ────────────────────────────────────────

export function useAvatarStats() {
  const [stats, setStats] = useState<AvatarStats>(loadStats);

  // Natural decay – every 60 s
  useEffect(() => {
    const id = setInterval(() => {
      setStats((prev) => {
        const next = applyNaturalDecay(prev);
        saveStats(next);
        return next;
      });
    }, 60_000);
    return () => clearInterval(id);
  }, []);

  const recordInteraction = useCallback(() => {
    setStats((prev) => {
      const next = applyInteractionBoost(prev);
      saveStats(next);
      return next;
    });
  }, []);

  return { stats, setStats, recordInteraction };
}

export default AvatarStatsPanel;
