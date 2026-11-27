extends RefCounted


static func percentile(values: Array, quantile: float) -> float:
	if values.is_empty():
		return 0.0
	var sorted_values: Array = values.duplicate()
	sorted_values.sort()
	var q := clampf(quantile, 0.0, 1.0)
	var rank := maxi(1, int(ceil(q * float(sorted_values.size()))))
	return float(sorted_values[mini(rank - 1, sorted_values.size() - 1)])


static func fps_for_frame_time(frame_time_ms: float) -> float:
	if frame_time_ms <= 0.0 or is_nan(frame_time_ms) or is_inf(frame_time_ms):
		return 0.0
	return 1000.0 / frame_time_ms


static func histogram_bucket(frame_time_ms: float) -> int:
	if frame_time_ms < 8.33:
		return 0
	if frame_time_ms < 12.5:
		return 1
	if frame_time_ms < 16.67:
		return 2
	if frame_time_ms < 22.0:
		return 3
	if frame_time_ms < 33.33:
		return 4
	if frame_time_ms < 50.0:
		return 5
	return 6


static func fisheye_map_u(
	output_u: float, strength: float, source_half_angle: float, edge_coverage: float
) -> float:
	if source_half_angle <= 0.0 or edge_coverage <= 0.0:
		return output_u
	var half_tan := tan(source_half_angle)
	if absf(half_tan) < 0.0001:
		return output_u
	var target_angle := atan(half_tan * clampf(edge_coverage, 0.01, 1.0))
	var centered := (output_u - 0.5) * 2.0
	var warped := tan(centered * target_angle) / half_tan
	return 0.5 + 0.5 * lerpf(centered, warped, clampf(strength, 0.0, 1.0))
