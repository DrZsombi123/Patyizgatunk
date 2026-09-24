shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear_mipmap;

// 0..1: wobble, zoom pulse, green tint (starts at 30% drunk)
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
// 0..1: blur + tunnel vision (reaches max at 90% drunk)
uniform float blindness : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec2 uv = SCREEN_UV;

	// Seasick warp
	float wobble = intensity * 0.012;
	uv.x += sin(uv.y * 12.0 + TIME * 1.8) * wobble;
	uv.y += cos(uv.x * 9.0 + TIME * 1.3) * wobble;

	// Slow breathing zoom
	float zoom = 1.0 - intensity * 0.04 * (0.5 + 0.5 * sin(TIME * 0.9));
	uv = (uv - 0.5) * zoom + 0.5;

	// Blur (mip level) and double vision (RGB split)
	float lod = intensity * 1.0 + blindness * 3.0;
	float split = intensity * 0.004 + blindness * 0.008;

	vec3 col;
	col.r = textureLod(screen_tex, uv + vec2(split, 0.0), lod).r;
	col.g = textureLod(screen_tex, uv, lod).g;
	col.b = textureLod(screen_tex, uv - vec2(split, 0.0), lod).b;

	// Sickly green tint
	col = mix(col, col * vec3(0.75, 1.05, 0.7), intensity * 0.7);

	// Tunnel vision
	float dist = distance(SCREEN_UV, vec2(0.5));
	float vig = smoothstep(0.75 - 0.55 * blindness, 1.0 - 0.35 * blindness, dist);
	col *= 1.0 - vig * 0.95;
	col *= 1.0 - blindness * 0.2;

	COLOR = vec4(col, 1.0);
}
