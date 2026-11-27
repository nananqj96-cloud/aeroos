extends RefCounted

const MathUtil = preload("res://scripts/benchmark_math.gd")


static func run() -> Array[String]:
	var failures: Array[String] = []
	var samples: Array[float] = [10.0, 20.0, 30.0, 40.0]
	if not is_equal_approx(MathUtil.percentile(samples, 0.5), 20.0):
		failures.append("nearest-rank median should be 20 ms")
	if not is_equal_approx(MathUtil.percentile(samples, 0.99), 40.0):
		failures.append("nearest-rank p99 should use the final sample")
	if not is_equal_approx(MathUtil.fps_for_frame_time(25.0), 40.0):
		failures.append("25 ms should equal 40 FPS")
	if MathUtil.fps_for_frame_time(0.0) != 0.0:
		failures.append("zero frame time must not produce a valid FPS result")
	if MathUtil.percentile([], 0.5) != 0.0:
		failures.append("empty sample lists must return zero for percentiles")
	if MathUtil.histogram_bucket(8.32) != 0 or MathUtil.histogram_bucket(8.33) != 1:
		failures.append("frame-time histogram must split at 8.33 ms")
	if MathUtil.histogram_bucket(50.0) != 6:
		failures.append("50 ms should land in the final histogram bucket")
	var center := MathUtil.fisheye_map_u(0.5, 1.0, 0.8, 0.93)
	var left := MathUtil.fisheye_map_u(0.0, 1.0, 0.8, 0.93)
	var right := MathUtil.fisheye_map_u(1.0, 1.0, 0.8, 0.93)
	if not is_equal_approx(center, 0.5):
		failures.append("horizontal lens remap must preserve the image center")
	if left <= 0.0 or right >= 1.0 or not is_equal_approx(left + right, 1.0):
		failures.append("edge coverage must keep fisheye samples inside the source")
	return failures
