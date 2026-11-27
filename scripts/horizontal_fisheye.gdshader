shader_type canvas_item;
render_mode unshaded;

uniform float distortion_strength : hint_range(0.0, 1.0) = 0.92;
uniform float edge_coverage : hint_range(0.65, 1.0) = 0.93;
uniform float source_half_tan = 1.0;
uniform float vertical_scale : hint_range(0.75, 1.25) = 1.0;

void fragment() {
	float half_tan = max(source_half_tan, 0.0001);
	float target_half_angle = atan(half_tan * clamp(edge_coverage, 0.01, 1.0));
	float centered_x = (UV.x - 0.5) * 2.0;
	float warped_x = tan(centered_x * target_half_angle) / half_tan;
	float mapped_x = mix(centered_x, warped_x, clamp(distortion_strength, 0.0, 1.0));
	float source_x = 0.5 + 0.5 * mapped_x;
	float source_y = 0.5 + (UV.y - 0.5) * vertical_scale;
	vec2 source_uv = clamp(vec2(source_x, source_y), vec2(0.001), vec2(0.999));
	COLOR = texture(TEXTURE, source_uv);
}
