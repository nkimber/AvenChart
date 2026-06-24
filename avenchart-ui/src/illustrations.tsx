// Hand-crafted, original flat-style decorative illustrations in the spirit of
// the "Calm Clinical" direction (no external assets fetched — kept dependency-free
// and license-free by drawing simple geometric shapes directly as SVG).

// A soft organic blob path, reused at different sizes/positions/opacities as a
// background layer behind each illustration's foreground shapes.
const BLOB_PATH =
  'M52.6,-58.5C66.7,-45.9,75.5,-26.6,77.4,-6.6C79.4,13.5,74.6,34.4,61.6,49.1C48.7,63.8,27.6,72.4,5.4,74.6C-16.8,76.9,-40.1,72.8,-56.7,59.6C-73.3,46.4,-83.1,24.1,-82.8,2.2C-82.5,-19.7,-72.1,-41.2,-55.4,-54.1C-38.7,-67,-15.8,-71.4,4.6,-70.6C25,-69.9,49.9,-63.9,52.6,-58.5Z'

function Blob({
  cx,
  cy,
  scale,
  opacity,
  fill = '#ffffff',
}: {
  cx: number
  cy: number
  scale: number
  opacity: number
  fill?: string
}) {
  return (
    <g transform={`translate(${cx} ${cy}) scale(${scale})`} opacity={opacity}>
      <path d={BLOB_PATH} fill={fill} />
    </g>
  )
}

function Sparkle({ x, y, size, opacity = 0.85 }: { x: number; y: number; size: number; opacity?: number }) {
  return (
    <path
      d={`M${x} ${y - size} L${x + size * 0.28} ${y - size * 0.28} L${x + size} ${y} L${x + size * 0.28} ${y + size * 0.28} L${x} ${y + size} L${x - size * 0.28} ${y + size * 0.28} L${x - size} ${y} L${x - size * 0.28} ${y - size * 0.28} Z`}
      fill="#ffffff"
      opacity={opacity}
    />
  )
}

/** Used on the clinician sign-in screen's hero panel. */
export function ClinicianIllustration() {
  return (
    <svg viewBox="0 0 300 260" xmlns="http://www.w3.org/2000/svg" role="presentation" aria-hidden="true">
      <Blob cx={150} cy={120} scale={1.45} opacity={0.1} />
      <Blob cx={210} cy={60} scale={0.55} opacity={0.14} />
      {/* clipboard */}
      <rect x="86" y="86" width="92" height="120" rx="12" fill="#ffffff" opacity="0.92" />
      <rect x="112" y="78" width="40" height="16" rx="6" fill="#ffffff" />
      <rect x="102" y="116" width="56" height="8" rx="4" fill="#0a5544" opacity="0.5" />
      <rect x="102" y="136" width="68" height="8" rx="4" fill="#0a5544" opacity="0.3" />
      <rect x="102" y="156" width="44" height="8" rx="4" fill="#0a5544" opacity="0.3" />
      <circle cx="148" cy="180" r="13" fill="#993c1d" opacity="0.85" />
      <path d="M142 180 l4 5 9 -11" stroke="#ffffff" strokeWidth="2.4" fill="none" strokeLinecap="round" strokeLinejoin="round" />
      {/* stethoscope */}
      <path
        d="M196 70 C196 95, 178 95, 178 118 C178 134, 192 144, 206 138"
        stroke="#ffffff"
        strokeWidth="6"
        fill="none"
        strokeLinecap="round"
        opacity="0.85"
      />
      <circle cx="196" cy="64" r="8" fill="#ffffff" opacity="0.85" />
      <circle cx="210" cy="140" r="10" fill="#ffffff" opacity="0.85" />
      <circle cx="210" cy="140" r="4" fill="#0a5544" opacity="0.6" />
      <Sparkle x={246} y={56} size={7} opacity={0.7} />
      <Sparkle x={70} y={200} size={5} opacity={0.55} />
    </svg>
  )
}

/** Used on the patient portal sign-in screen's hero panel. */
export function PatientIllustration() {
  return (
    <svg viewBox="0 0 300 260" xmlns="http://www.w3.org/2000/svg" role="presentation" aria-hidden="true">
      <Blob cx={150} cy={130} scale={1.45} opacity={0.1} />
      <Blob cx={90} cy={70} scale={0.5} opacity={0.14} />
      {/* phone / portal device */}
      <rect x="104" y="58" width="92" height="156" rx="18" fill="#ffffff" opacity="0.92" />
      <rect x="118" y="78" width="64" height="86" rx="8" fill="#0a5544" opacity="0.16" />
      {/* heartbeat pulse line */}
      <path
        d="M124 122 H140 L150 102 L160 142 L170 118 L182 118"
        stroke="#14a37e"
        strokeWidth="4"
        fill="none"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="150" cy="190" r="9" fill="#0a5544" opacity="0.4" />
      <rect x="134" y="68" width="32" height="6" rx="3" fill="#0a5544" opacity="0.3" />
      {/* small care/notification badge */}
      <circle cx="206" cy="70" r="17" fill="#993c1d" opacity="0.9" />
      <path d="M198 70 a8 8 0 1 1 16 0 c0 5 -8 11 -8 11 s-8 -6 -8 -11" fill="#ffffff" />
      <Sparkle x={62} y={150} size={7} opacity={0.6} />
      <Sparkle x={232} y={188} size={6} opacity={0.55} />
    </svg>
  )
}

/** Used on the entry chooser screen's hero panel. */
export function WelcomeIllustration() {
  return (
    <svg viewBox="0 0 300 260" xmlns="http://www.w3.org/2000/svg" role="presentation" aria-hidden="true">
      <Blob cx={150} cy={125} scale={1.5} opacity={0.1} />
      <Blob cx={206} cy={170} scale={0.5} opacity={0.14} />
      {/* shield emblem */}
      <path
        d="M150 56 L210 80 C210 130 188 168 150 188 C112 168 90 130 90 80 Z"
        fill="#ffffff"
        opacity="0.92"
      />
      <path
        d="M124 122 H140 L148 102 L160 144 L168 118 L178 118"
        stroke="#0a5544"
        strokeWidth="5"
        fill="none"
        strokeLinecap="round"
        strokeLinejoin="round"
        opacity="0.7"
      />
      <Sparkle x={92} y={70} size={7} opacity={0.65} />
      <Sparkle x={224} y={108} size={8} opacity={0.6} />
      <Sparkle x={208} y={206} size={5} opacity={0.5} />
    </svg>
  )
}

/** Smaller accent illustration tucked into the corner of dashboard hero banners. */
export function PulseBadgeIllustration() {
  return (
    <svg viewBox="0 0 200 200" xmlns="http://www.w3.org/2000/svg" role="presentation" aria-hidden="true">
      <Blob cx={100} cy={100} scale={1} opacity={0.12} />
      <circle cx="100" cy="100" r="58" fill="#ffffff" opacity="0.14" />
      <circle cx="100" cy="100" r="40" fill="#ffffff" opacity="0.18" />
      <path
        d="M68 100 H86 L96 76 L110 124 L122 100 L134 100"
        stroke="#ffffff"
        strokeWidth="6"
        fill="none"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <Sparkle x={146} y={56} size={7} opacity={0.55} />
      <Sparkle x={54} y={146} size={6} opacity={0.45} />
    </svg>
  )
}
