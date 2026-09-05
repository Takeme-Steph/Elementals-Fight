import React, {
  useState,
  useMemo,
  useRef,
  useCallback,
  useEffect,
  memo,
} from "react";
import {
  motion,
  AnimatePresence,
  useMotionValue,
  useTransform,
  useReducedMotion,
  animate,
} from "framer-motion";
import { ChevronLeft, ShoppingBag, Gem, Swords } from "lucide-react";
import { CHARACTERS, STAT_AXES, STAT_MAX } from "./data/characters.js";

// ============================================================================
// Constants
// ============================================================================

const ICON = 64; // carousel icon diameter (px)
const GAP = 20; // gap between carousel icons (px)
const STEP = ICON + GAP; // 84px center-to-center spacing

// ============================================================================
// Small utilities
// ============================================================================

const clamp = (v, min, max) => Math.min(max, Math.max(min, v));

/** Mix two hex colours (0..1 amount of b). Used for radial gradients. */
function mixHex(a, b, amt) {
  const pa = parseInt(a.slice(1), 16);
  const pb = parseInt(b.slice(1), 16);
  const ar = (pa >> 16) & 255,
    ag = (pa >> 8) & 255,
    ab = pa & 255;
  const br = (pb >> 16) & 255,
    bg = (pb >> 8) & 255,
    bb = pb & 255;
  const r = Math.round(ar + (br - ar) * amt);
  const g = Math.round(ag + (bg - ag) * amt);
  const bl = Math.round(ab + (bb - ab) * amt);
  return `rgb(${r}, ${g}, ${bl})`;
}

// ============================================================================
// Backdrop - ambient gradient blobs, starfield, particles, horizon glow
// ============================================================================

const Backdrop = memo(function Backdrop({ character, reduceMotion }) {
  const { primary, secondary, glow, deep } = character.colors;

  // Particle params generated once and reused across theme changes.
  const particles = useMemo(() => {
    return Array.from({ length: 16 }, (_, i) => ({
      id: i,
      left: Math.random() * 100,
      size: 2 + Math.random() * 3,
      duration: 9 + Math.random() * 10,
      delay: Math.random() * -14,
      drift: (Math.random() - 0.5) * 60,
      opacity: 0.4 + Math.random() * 0.5,
      isPrimary: i % 3 === 0,
    }));
  }, []);

  const blobs = useMemo(
    () => [
      {
        id: "a",
        top: "8%",
        left: "12%",
        w: 320,
        h: 320,
        colorKey: "glow",
        duration: 11,
        rangeX: [0, 40, -10, 0],
        rangeY: [0, -30, 20, 0],
        rangeS: [1, 1.15, 0.95, 1],
      },
      {
        id: "b",
        top: "55%",
        left: "72%",
        w: 380,
        h: 380,
        colorKey: "primary",
        duration: 13,
        rangeX: [0, -30, 25, 0],
        rangeY: [0, 25, -20, 0],
        rangeS: [1, 0.9, 1.1, 1],
      },
      {
        id: "c",
        top: "70%",
        left: "20%",
        w: 260,
        h: 260,
        colorKey: "secondary",
        duration: 9.5,
        rangeX: [0, 20, -25, 0],
        rangeY: [0, -20, 15, 0],
        rangeS: [1, 1.08, 0.92, 1],
      },
      {
        id: "d",
        top: "18%",
        left: "68%",
        w: 220,
        h: 220,
        colorKey: "glow",
        duration: 14,
        rangeX: [0, -20, 30, 0],
        rangeY: [0, 20, -25, 0],
        rangeS: [1, 1.1, 0.95, 1],
      },
    ],
    []
  );

  const bgStyle = useMemo(
    () => ({
      background: `
        radial-gradient(120% 90% at 68% 78%, ${mixHex(glow, deep, 0.35)}55 0%, transparent 60%),
        radial-gradient(140% 100% at 20% 10%, ${mixHex(primary, deep, 0.55)}33 0%, transparent 55%),
        linear-gradient(180deg, #030712 0%, ${deep} 55%, #030712 100%)
      `,
    }),
    [primary, deep, glow]
  );

  const starStyle = useMemo(
    () => ({
      backgroundImage: `
        radial-gradient(1.5px 1.5px at 15% 25%, rgba(255,255,255,0.55) 50%, transparent 100%),
        radial-gradient(1.5px 1.5px at 75% 15%, rgba(255,255,255,0.4) 50%, transparent 100%),
        radial-gradient(1px 1px at 35% 65%, rgba(255,255,255,0.5) 50%, transparent 100%),
        radial-gradient(1px 1px at 85% 55%, rgba(255,255,255,0.35) 50%, transparent 100%),
        radial-gradient(1.5px 1.5px at 55% 35%, rgba(255,255,255,0.45) 50%, transparent 100%),
        radial-gradient(1px 1px at 5% 80%, rgba(255,255,255,0.3) 50%, transparent 100%),
        radial-gradient(1px 1px at 95% 85%, rgba(255,255,255,0.35) 50%, transparent 100%),
        radial-gradient(1.5px 1.5px at 45% 90%, rgba(255,255,255,0.3) 50%, transparent 100%)
      `,
      backgroundSize: "220px 220px",
      backgroundRepeat: "repeat",
      opacity: 0.5,
    }),
    []
  );

  return (
    <div className="absolute inset-0 z-0 overflow-hidden pointer-events-none">
      <motion.div
        className="absolute inset-0"
        animate={{ backgroundColor: "transparent" }}
        style={bgStyle}
        transition={{ duration: 0.6 }}
      />
      <div className="absolute inset-0" style={starStyle} />

      {/* Horizon glow behind the deity */}
      <motion.div
        className="absolute rounded-full mix-blend-screen blur-3xl"
        style={{
          width: "70vw",
          height: "40vh",
          left: "40%",
          bottom: "-12%",
          background: `radial-gradient(closest-side, ${primary}55, transparent 70%)`,
        }}
        animate={{ opacity: [0.35, 0.55, 0.35] }}
        transition={{ duration: 6, repeat: reduceMotion ? 0 : Infinity, repeatType: "mirror" }}
      />

      {blobs.map((b) => (
        <motion.div
          key={b.id}
          className="absolute rounded-full mix-blend-screen blur-3xl will-change-transform"
          style={{
            top: b.top,
            left: b.left,
            width: b.w,
            height: b.h,
            backgroundColor: character.colors[b.colorKey],
          }}
          animate={
            reduceMotion
              ? { opacity: 0.4 }
              : {
                  x: b.rangeX,
                  y: b.rangeY,
                  scale: b.rangeS,
                  opacity: [0.35, 0.55, 0.4, 0.35],
                  backgroundColor: character.colors[b.colorKey],
                }
          }
          transition={
            reduceMotion
              ? { duration: 0.6 }
              : {
                  x: { duration: b.duration, repeat: Infinity, repeatType: "mirror", ease: "easeInOut" },
                  y: { duration: b.duration * 1.15, repeat: Infinity, repeatType: "mirror", ease: "easeInOut" },
                  scale: { duration: b.duration * 0.9, repeat: Infinity, repeatType: "mirror", ease: "easeInOut" },
                  opacity: { duration: b.duration, repeat: Infinity, repeatType: "mirror", ease: "easeInOut" },
                  backgroundColor: { duration: 0.6 },
                }
          }
        />
      ))}

      {!reduceMotion &&
        particles.map((p) => (
          <span
            key={p.id}
            className="particle"
            style={{
              left: `${p.left}%`,
              width: p.size,
              height: p.size,
              backgroundColor: p.isPrimary ? primary : "#ffffff",
              animationDuration: `${p.duration}s`,
              animationDelay: `${p.delay}s`,
              "--p-drift": `${p.drift}px`,
              "--p-opacity": p.opacity,
            }}
          />
        ))}
    </div>
  );
});

// ============================================================================
// IconButton - reusable round glass button
// ============================================================================

const IconButton = memo(function IconButton({ icon, onClick, label, className = "" }) {
  return (
    <motion.button
      type="button"
      onClick={onClick}
      aria-label={label}
      whileTap={{ scale: 0.88 }}
      whileHover={{ scale: 1.06 }}
      transition={{ type: "spring", stiffness: 400, damping: 17 }}
      className={
        "flex items-center justify-center rounded-full bg-black/40 backdrop-blur-md border border-white/15 text-white shadow-[0_0_18px_-6px_rgba(255,255,255,0.4)] " +
        className
      }
    >
      {icon}
    </motion.button>
  );
});

// ============================================================================
// TopBar
// ============================================================================

const TopBar = memo(function TopBar() {
  return (
    <div className="absolute top-0 left-0 right-0 z-30 flex items-center justify-between px-3 pt-[max(0.5rem,env(safe-area-inset-top))]">
      <div className="flex items-center gap-3">
        <IconButton icon={<ChevronLeft size={18} />} label="Back" className="w-9 h-9" />
        <span className="text-[10px] font-semibold tracking-[0.25em] text-[#f5d76e] uppercase">
          Choose Your Deity
        </span>
      </div>
      <div className="flex items-center gap-2">
        <div className="flex items-center gap-1.5 rounded-full bg-black/40 backdrop-blur-md border border-white/15 px-3 py-1.5">
          <Gem size={14} className="text-cyan-300" />
          <span className="text-xs font-bold text-white tabular-nums">1,250</span>
        </div>
        <IconButton icon={<ShoppingBag size={16} />} label="Shop" className="w-9 h-9" />
      </div>
    </div>
  );
});

// ============================================================================
// LorePanel - left column, animated text rewrite on character change
// ============================================================================

const lineVariants = {
  hidden: { opacity: 0, y: 8, filter: "blur(4px)" },
  show: { opacity: 1, y: 0, filter: "blur(0px)", transition: { duration: 0.22 } },
  exit: { opacity: 0, y: -6, transition: { duration: 0.15 } },
};

const containerVariants = {
  hidden: {},
  show: { transition: { staggerChildren: 0.05 } },
  exit: { transition: { staggerChildren: 0.03 } },
};

const LorePanel = memo(function LorePanel({ character }) {
  const { primary, secondary } = character.colors;
  return (
    <div className="absolute left-3 sm:left-4 top-[45%] -translate-y-1/2 z-20 w-[min(36vw,20rem)] max-w-xs">
      <div
        className="rounded-2xl p-[1px]"
        style={{
          background: `linear-gradient(180deg, ${primary}b3 0%, ${primary}55 45%, transparent 100%)`,
          boxShadow: `0 0 40px -10px ${primary}80`,
        }}
      >
        <div className="rounded-2xl bg-black/45 backdrop-blur-md px-4 py-4 overflow-hidden">
          <AnimatePresence mode="wait">
            <motion.div
              key={character.id}
              variants={containerVariants}
              initial="hidden"
              animate="show"
              exit="exit"
            >
              <motion.div variants={lineVariants} className="flex items-center gap-2 mb-1">
                <span className="text-[9px] font-bold tracking-[0.2em] uppercase text-[#f5d76e]/90">
                  {character.pantheon} · {character.domain}
                </span>
                {character.placeholder && (
                  <span className="text-[8px] font-bold tracking-widest uppercase text-amber-300 bg-amber-400/15 border border-amber-300/40 rounded-full px-1.5 py-[1px]">
                    Placeholder
                  </span>
                )}
              </motion.div>

              <motion.h1
                variants={lineVariants}
                className="text-3xl font-black uppercase leading-[1.05] bg-clip-text text-transparent"
                style={{
                  backgroundImage: `linear-gradient(135deg, #ffffff 0%, ${primary} 100%)`,
                }}
              >
                {character.name}
              </motion.h1>

              <motion.p
                variants={lineVariants}
                className="italic text-sm mb-2 mt-0.5"
                style={{ color: secondary }}
              >
                {character.title}
              </motion.p>

              <motion.p
                variants={lineVariants}
                className="text-[11px] leading-snug text-white/80 mb-3 line-clamp-4"
              >
                {character.lore}
              </motion.p>

              <motion.div variants={lineVariants} className="flex flex-wrap gap-1.5">
                <span
                  className="rounded-full border text-[10px] uppercase tracking-wider px-2.5 py-1 font-semibold"
                  style={{
                    borderColor: `${primary}99`,
                    backgroundColor: `${primary}26`,
                    color: primary,
                  }}
                >
                  {character.playstyle}
                </span>
                <span
                  className="rounded-full border text-[10px] uppercase tracking-wider px-2.5 py-1 font-semibold"
                  style={{
                    borderColor: `${secondary}66`,
                    backgroundColor: `${secondary}1a`,
                    color: secondary,
                  }}
                >
                  {character.element}
                </span>
              </motion.div>
            </motion.div>
          </AnimatePresence>
        </div>
      </div>
    </div>
  );
});

// ============================================================================
// DeityStage - center presentation of the active character
// ============================================================================

const DeityStage = memo(function DeityStage({ character, reduceMotion }) {
  const { primary, secondary, glow } = character.colors;

  return (
    <div className="absolute left-[62%] sm:left-[65%] -translate-x-1/2 bottom-[7.5rem] top-10 z-10 flex items-end justify-center pointer-events-none">
      <AnimatePresence mode="popLayout">
        <motion.div
          key={character.id}
          initial={{ opacity: 0, scale: 0.82, y: 24 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 1.08, filter: "blur(6px)" }}
          transition={{ type: "spring", stiffness: 260, damping: 20 }}
          className="relative flex flex-col items-center justify-end h-full"
        >
          <motion.div
            className="relative flex items-center justify-center"
            animate={reduceMotion ? {} : { y: [0, -6, 0] }}
            transition={{ duration: 3.5, repeat: reduceMotion ? 0 : Infinity, ease: "easeInOut" }}
          >
            {/* Halo: a hot core plus a wide bloom so the figure reads as lit from within */}
            <motion.div
              className="absolute rounded-full blur-2xl mix-blend-screen"
              style={{
                width: "min(64vh, 360px)",
                height: "min(64vh, 360px)",
                background: `radial-gradient(circle, ${secondary}cc 0%, ${primary}b0 22%, ${glow}66 45%, transparent 70%)`,
              }}
              animate={reduceMotion ? { opacity: 0.8 } : { opacity: [0.7, 1, 0.7], scale: [1, 1.08, 1] }}
              transition={{ duration: 4, repeat: reduceMotion ? 0 : Infinity, ease: "easeInOut" }}
            />
            {/* Sun-ray shards behind the figure (conic gradient, rotates slowly) */}
            {!reduceMotion && (
              <div
                className="absolute rounded-full spin-slow mix-blend-screen"
                style={{
                  width: "min(70vh, 400px)",
                  height: "min(70vh, 400px)",
                  opacity: 0.35,
                  background: `conic-gradient(from 0deg, transparent 0deg, ${primary}66 8deg, transparent 16deg, transparent 40deg, ${secondary}55 48deg, transparent 56deg, transparent 90deg, ${primary}66 98deg, transparent 106deg, transparent 140deg, ${secondary}55 148deg, transparent 156deg, transparent 200deg, ${primary}66 208deg, transparent 216deg, transparent 250deg, ${secondary}55 258deg, transparent 266deg, transparent 300deg, ${primary}66 308deg, transparent 316deg, transparent 360deg)`,
                  maskImage: "radial-gradient(circle, black 20%, transparent 70%)",
                  WebkitMaskImage: "radial-gradient(circle, black 20%, transparent 70%)",
                }}
              />
            )}

            {/* Light pillar */}
            <div
              className="absolute bottom-0"
              style={{
                width: 10,
                height: "min(58vh, 320px)",
                background: `linear-gradient(180deg, transparent 0%, ${secondary}55 40%, ${primary}88 100%)`,
                filter: "blur(6px)",
              }}
            />

            {/* Orbiting rings */}
            <svg
              width="min(46vh, 260px)"
              height="min(46vh, 260px)"
              viewBox="0 0 260 260"
              className="absolute spin-slow"
              style={{ width: "min(46vh, 260px)", height: "min(46vh, 260px)" }}
            >
              <ellipse
                cx="130"
                cy="130"
                rx="120"
                ry="46"
                fill="none"
                stroke={primary}
                strokeOpacity="0.45"
                strokeWidth="1.5"
              />
            </svg>
            <svg
              width="min(40vh, 220px)"
              height="min(40vh, 220px)"
              viewBox="0 0 220 220"
              className="absolute spin-slow-reverse"
              style={{ width: "min(40vh, 220px)", height: "min(40vh, 220px)" }}
            >
              <ellipse
                cx="110"
                cy="110"
                rx="60"
                ry="100"
                fill="none"
                stroke={secondary}
                strokeOpacity="0.35"
                strokeWidth="1.5"
              />
            </svg>

            {character.portrait ? (
              <img
                src={character.portrait}
                alt={character.name}
                className="relative object-contain"
                style={{ maxHeight: "68dvh", filter: `drop-shadow(0 0 30px ${primary}aa)` }}
              />
            ) : (
              <span
                className="relative font-black select-none"
                style={{
                  fontSize: "clamp(5rem, 16vh, 9rem)",
                  lineHeight: 1,
                  backgroundImage: `linear-gradient(160deg, ${secondary} 0%, ${primary} 100%)`,
                  WebkitBackgroundClip: "text",
                  backgroundClip: "text",
                  color: "transparent",
                  filter: `drop-shadow(0 0 30px ${primary}aa)`,
                }}
              >
                {character.glyph}
              </span>
            )}
          </motion.div>

          {/* Pedestal: a flat gold/primary ring under the figure so the stage reads as a floor */}
          <div className="relative flex items-center justify-center -mt-2">
            <div
              className="absolute rounded-full blur-xl"
              style={{
                width: "min(44vh, 220px)",
                height: 30,
                backgroundColor: `${primary}66`,
              }}
            />
            <svg
              viewBox="0 0 240 60"
              style={{ width: "min(48vh, 240px)", height: "auto" }}
              className="relative"
            >
              <ellipse cx="120" cy="30" rx="112" ry="22" fill="none" stroke="#f5d76e" strokeOpacity="0.55" strokeWidth="1.5" />
              <ellipse cx="120" cy="30" rx="96" ry="18" fill={`${primary}22`} stroke={primary} strokeOpacity="0.8" strokeWidth="2" strokeDasharray="6 10" className="spin-slow-reverse" style={{ transformOrigin: "120px 30px" }} />
              <ellipse cx="120" cy="30" rx="60" ry="10" fill={`${secondary}33`} />
            </svg>
          </div>
        </motion.div>
      </AnimatePresence>
    </div>
  );
});

// ============================================================================
// RadarChart - SVG polygon that morphs between characters
// ============================================================================

function statsToPoints(stats, size, center) {
  const n = STAT_AXES.length;
  return STAT_AXES.map((axis, i) => {
    const angle = (Math.PI * 2 * i) / n - Math.PI / 2;
    const value = clamp(stats[axis.key] ?? 0, 0, STAT_MAX) / STAT_MAX;
    const r = value * size;
    return {
      x: center + r * Math.cos(angle),
      y: center + r * Math.sin(angle),
    };
  });
}

function pointsToPath(points) {
  return (
    points.map((p, i) => `${i === 0 ? "M" : "L"} ${p.x.toFixed(2)} ${p.y.toFixed(2)}`).join(" ") + " Z"
  );
}

const RadarChart = memo(function RadarChart({ character, size = 160 }) {
  const { primary, secondary } = character.colors;
  const center = size / 2;
  const radius = size * 0.36;
  const labelRadius = radius + 16;
  const n = STAT_AXES.length;

  const guides = [0.33, 0.66, 1];

  const dataPath = useMemo(() => {
    const pts = statsToPoints(character.stats, radius, center);
    return pointsToPath(pts);
  }, [character.stats, radius, center]);

  const vertices = useMemo(() => statsToPoints(character.stats, radius, center), [
    character.stats,
    radius,
    center,
  ]);

  const overall = useMemo(() => {
    const vals = STAT_AXES.map((a) => character.stats[a.key] ?? 0);
    return vals.reduce((s, v) => s + v, 0) / vals.length;
  }, [character.stats]);

  const ovrMotion = useMotionValue(0);
  const ovrRounded = useTransform(ovrMotion, (v) => v.toFixed(1));
  const [ovrDisplay, setOvrDisplay] = useState("0.0");

  useEffect(() => {
    const controls = animate(ovrMotion, overall, {
      duration: 0.7,
      ease: "easeOut",
      onUpdate: (v) => setOvrDisplay(v.toFixed(1)),
    });
    return () => controls.stop();
  }, [overall, ovrMotion]);

  const axisLabelPositions = useMemo(
    () =>
      STAT_AXES.map((axis, i) => {
        const angle = (Math.PI * 2 * i) / n - Math.PI / 2;
        return {
          key: axis.key,
          label: axis.label,
          x: center + labelRadius * Math.cos(angle),
          y: center + labelRadius * Math.sin(angle),
          isMax: axis.key === maxStatKey(character.stats),
        };
      }),
    [center, labelRadius, n, character.stats]
  );

  return (
    <div className="flex flex-col items-center select-none">
      <span className="text-[9px] font-bold tracking-[0.25em] uppercase text-white/60 mb-1">
        Attributes
      </span>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} className="overflow-visible">
        <defs>
          <filter id="radarGlow" x="-60%" y="-60%" width="220%" height="220%">
            <feGaussianBlur stdDeviation="4.5" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        {/* Guide pentagons */}
        {guides.map((g) => (
          <polygon
            key={g}
            points={statsToPoints(
              Object.fromEntries(STAT_AXES.map((a) => [a.key, STAT_MAX * g])),
              radius,
              center
            )
              .map((p) => `${p.x},${p.y}`)
              .join(" ")}
            fill="none"
            stroke="rgba(255,255,255,0.15)"
            strokeWidth="1"
          />
        ))}

        {/* Spokes */}
        {STAT_AXES.map((axis, i) => {
          const angle = (Math.PI * 2 * i) / n - Math.PI / 2;
          const x2 = center + radius * Math.cos(angle);
          const y2 = center + radius * Math.sin(angle);
          return (
            <line
              key={axis.key}
              x1={center}
              y1={center}
              x2={x2}
              y2={y2}
              stroke="rgba(255,255,255,0.15)"
              strokeWidth="1"
            />
          );
        })}

        {/* Glow copy behind */}
        <motion.path
          initial={{ d: dataPath }}
          animate={{ d: dataPath }}
          transition={{ type: "spring", stiffness: 120, damping: 16 }}
          fill={primary}
          fillOpacity="0.35"
          stroke={primary}
          strokeWidth="3"
          filter="url(#radarGlow)"
          opacity="0.55"
        />

        {/* Main data polygon */}
        <motion.path
          initial={{ d: dataPath }}
          animate={{ d: dataPath }}
          transition={{ type: "spring", stiffness: 120, damping: 16 }}
          fill={primary}
          fillOpacity="0.25"
          stroke={primary}
          strokeWidth="2"
        />

        {/* Vertex dots */}
        {vertices.map((v, i) => (
          <motion.circle
            key={STAT_AXES[i].key}
            initial={{ cx: v.x, cy: v.y }}
            animate={{ cx: v.x, cy: v.y }}
            transition={{ type: "spring", stiffness: 120, damping: 16 }}
            r="3.5"
            fill={secondary}
            stroke={primary}
            strokeWidth="1"
          />
        ))}

        {/* Axis labels */}
        {axisLabelPositions.map((p) => (
          <text
            key={p.key}
            x={p.x}
            y={p.y}
            textAnchor="middle"
            dominantBaseline="middle"
            fontSize="9"
            fontWeight="700"
            letterSpacing="0.5"
            fill={p.isMax ? primary : "rgba(255,255,255,0.7)"}
          >
            {STAT_AXES.find((a) => a.key === p.key).label}
          </text>
        ))}
      </svg>

      <div className="flex flex-col items-center -mt-1">
        <span className="text-2xl font-black text-white tabular-nums leading-none">{ovrDisplay}</span>
        <span className="text-[8px] font-bold tracking-[0.2em] uppercase text-white/50">OVR</span>
      </div>
    </div>
  );
});

function maxStatKey(stats) {
  let bestKey = STAT_AXES[0].key;
  let bestVal = -Infinity;
  for (const axis of STAT_AXES) {
    const v = stats[axis.key] ?? 0;
    if (v > bestVal) {
      bestVal = v;
      bestKey = axis.key;
    }
  }
  return bestKey;
}

// ============================================================================
// Carousel - elastic drag-to-select roster strip
// ============================================================================

const CarouselIcon = memo(function CarouselIcon({ character, isSelected, onSelect, index }) {
  const { primary, deep } = character.colors;

  return (
    <motion.button
      type="button"
      onClick={() => onSelect(index)}
      className="relative flex-shrink-0 flex items-center justify-center rounded-full"
      style={{ width: ICON, height: ICON }}
      animate={{
        scale: isSelected ? 1.25 : 0.85,
        opacity: isSelected ? 1 : 0.55,
        zIndex: isSelected ? 10 : 1,
      }}
      transition={{ type: "spring", stiffness: 300, damping: 18 }}
    >
      {isSelected && (
        <svg
          className="absolute inset-[-9px] spin-slow"
          width={ICON + 18}
          height={ICON + 18}
          viewBox={`0 0 ${ICON + 18} ${ICON + 18}`}
        >
          <circle
            cx={(ICON + 18) / 2}
            cy={(ICON + 18) / 2}
            r={(ICON + 18) / 2 - 2}
            fill="none"
            stroke={primary}
            strokeWidth="1.5"
            strokeDasharray="4 5"
            opacity="0.8"
          />
        </svg>
      )}
      <div
        className="w-full h-full rounded-full flex items-center justify-center overflow-hidden"
        style={{
          boxShadow: isSelected ? `0 0 24px ${primary}` : "none",
          outline: isSelected ? `2px solid ${primary}` : "1px solid rgba(255,255,255,0.2)",
          filter: isSelected ? "none" : "grayscale(35%)",
          background: character.icon
            ? "transparent"
            : `radial-gradient(circle at 35% 30%, ${primary}, ${deep})`,
        }}
      >
        {character.icon ? (
          <img src={character.icon} alt={character.name} className="w-full h-full object-cover" />
        ) : (
          <span className="text-xl font-black text-white drop-shadow">{character.glyph}</span>
        )}
      </div>
    </motion.button>
  );
});

function Carousel({ characters, selectedIndex, onSelect }) {
  const containerRef = useRef(null);
  const [containerWidth, setContainerWidth] = useState(0);
  const x = useMotionValue(0);
  const dragStartX = useRef(0);
  const didDrag = useRef(false);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const update = () => setContainerWidth(el.clientWidth);
    update();
    const ro = new ResizeObserver(update);
    ro.observe(el);
    window.addEventListener("resize", update);
    return () => {
      ro.disconnect();
      window.removeEventListener("resize", update);
    };
  }, []);

  const centerOffset = containerWidth / 2 - ICON / 2;

  const xFor = useCallback((idx) => centerOffset - idx * STEP, [centerOffset]);

  useEffect(() => {
    if (!containerWidth) return;
    animate(x, xFor(selectedIndex), { type: "spring", stiffness: 380, damping: 32 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [containerWidth]);

  const select = useCallback(
    (idx) => {
      const clamped = clamp(idx, 0, characters.length - 1);
      onSelect(clamped);
      animate(x, xFor(clamped), { type: "spring", stiffness: 380, damping: 32 });
    },
    [characters.length, onSelect, x, xFor]
  );

  const handleDragStart = useCallback(() => {
    dragStartX.current = x.get();
    didDrag.current = false;
  }, [x]);

  const handleDrag = useCallback(() => {
    if (Math.abs(x.get() - dragStartX.current) > 5) {
      didDrag.current = true;
    }
  }, [x]);

  const handleDragEnd = useCallback(
    (e, info) => {
      const projected = x.get() + info.velocity.x * 0.2;
      const idx = clamp(Math.round((centerOffset - projected) / STEP), 0, characters.length - 1);
      select(idx);
    },
    [centerOffset, characters.length, select]
  );

  const handleTap = useCallback(
    (idx) => {
      if (didDrag.current) return;
      select(idx);
    },
    [select]
  );

  const handleKeyDown = useCallback(
    (e) => {
      if (e.key === "ArrowLeft") select(selectedIndex - 1);
      else if (e.key === "ArrowRight") select(selectedIndex + 1);
    },
    [select, selectedIndex]
  );

  const minX = containerWidth ? xFor(characters.length - 1) : 0;
  const maxX = containerWidth ? xFor(0) : 0;

  return (
    <div
      ref={containerRef}
      className="relative w-full h-full overflow-visible"
      style={{ touchAction: "pan-x" }}
      tabIndex={0}
      onKeyDown={handleKeyDown}
    >
      {containerWidth > 0 && (
        <motion.div
          className="absolute top-0 flex items-center"
          style={{ x, height: "100%", gap: GAP }}
          drag="x"
          dragConstraints={{ left: minX, right: maxX }}
          dragElastic={0.25}
          dragMomentum={false}
          onDragStart={handleDragStart}
          onDrag={handleDrag}
          onDragEnd={handleDragEnd}
        >
          {characters.map((c, i) => (
            <div key={c.id} onClick={() => handleTap(i)}>
              <CarouselIcon character={c} isSelected={i === selectedIndex} onSelect={handleTap} index={i} />
            </div>
          ))}
        </motion.div>
      )}
    </div>
  );
}

// ============================================================================
// ConfirmButton
// ============================================================================

function ConfirmButton({ character, onConfirm, reduceMotion }) {
  return (
    <motion.button
      type="button"
      onClick={onConfirm}
      whileTap={{ scale: 0.92 }}
      animate={
        reduceMotion
          ? {}
          : {
              scale: [1, 1.04, 1],
            }
      }
      transition={{ duration: 1.8, repeat: reduceMotion ? 0 : Infinity, ease: "easeInOut" }}
      className="flex items-center gap-2 px-7 py-3 rounded-2xl font-black tracking-widest uppercase text-black text-sm"
      style={{
        backgroundImage: "linear-gradient(120deg, #f5d76e 0%, var(--c-primary) 100%)",
        boxShadow: "0 0 30px -4px var(--c-primary)",
      }}
    >
      <Swords size={16} />
      Confirm
    </motion.button>
  );
}

// ============================================================================
// Root component
// ============================================================================

export default function CharacterSelect() {
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [confirmed, setConfirmed] = useState(false);
  const [toast, setToast] = useState(false);
  const reduceMotion = useReducedMotion();

  const character = CHARACTERS[selectedIndex];

  const handleSelect = useCallback((idx) => {
    setSelectedIndex(idx);
  }, []);

  const handleConfirm = useCallback(() => {
    setConfirmed(true);
    setToast(true);
    // eslint-disable-next-line no-console
    console.log("Character confirmed. unityIndex =", character.unityIndex, character.id);
    const t1 = setTimeout(() => setConfirmed(false), 500);
    const t2 = setTimeout(() => setToast(false), 1200);
    return () => {
      clearTimeout(t1);
      clearTimeout(t2);
    };
  }, [character]);

  const rootVars = useMemo(
    () => ({
      "--c-primary": character.colors.primary,
      "--c-secondary": character.colors.secondary,
      "--c-glow": character.colors.glow,
      "--c-deep": character.colors.deep,
    }),
    [character]
  );

  const chartSize = 160;

  return (
    <div
      className="relative h-[100dvh] w-screen overflow-hidden select-none touch-pan-x bg-[#030712]"
      style={rootVars}
    >
      <Backdrop character={character} reduceMotion={reduceMotion} />

      <TopBar />

      <LorePanel character={character} />

      <DeityStage character={character} reduceMotion={reduceMotion} />

      <div
        className="absolute z-20 flex items-center justify-center"
        style={{
          right: "1rem",
          top: "50%",
          transform: "translateY(-50%)",
        }}
      >
        <div
          className="max-[400px]:scale-[0.75]"
          style={{ transformOrigin: "center" }}
        >
          <RadarChart character={character} size={chartSize} />
        </div>
      </div>

      {/* Bottom carousel + confirm */}
      <div className="absolute bottom-3 left-0 right-40 z-30" style={{ height: 92 }}>
        <Carousel characters={CHARACTERS} selectedIndex={selectedIndex} onSelect={handleSelect} />
      </div>

      <div className="absolute bottom-4 right-4 z-30">
        <ConfirmButton character={character} onConfirm={handleConfirm} reduceMotion={reduceMotion} />
      </div>

      {/* Confirm flash */}
      <AnimatePresence>
        {confirmed && (
          <motion.div
            className="absolute inset-0 z-40 pointer-events-none"
            style={{
              background: `radial-gradient(circle at 65% 55%, white 0%, transparent 60%)`,
            }}
            initial={{ opacity: 0.6 }}
            animate={{ opacity: 0 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.5 }}
          />
        )}
      </AnimatePresence>

      {/* Toast */}
      <AnimatePresence>
        {toast && (
          <motion.div
            className="absolute top-3 left-1/2 -translate-x-1/2 z-50 px-4 py-2 rounded-full bg-black/70 backdrop-blur-md border text-xs font-black tracking-widest uppercase text-white"
            style={{ borderColor: character.colors.primary }}
            initial={{ opacity: 0, y: -12, scale: 0.9 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -12, scale: 0.9 }}
            transition={{ type: "spring", stiffness: 400, damping: 24 }}
          >
            {character.name.toUpperCase()} SELECTED
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
