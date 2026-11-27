extends Control

const BenchmarkMath = preload("res://scripts/benchmark_math.gd")
const Phase0Tests = preload("res://scripts/tests.gd")
const FishEyeShader = preload("res://scripts/horizontal_fisheye.gdshader")

const TRAFFIC_COUNT := 50
const TRAFFIC_SEED := 20261127
const ROAD_LENGTH := 1000.0
const BENCHMARK_SPEED_KPH := 80.0
const BENCHMARK_START_Z := -420.0
const BENCHMARK_WARMUP_SECONDS := 5.0
const BENCHMARK_SECONDS_DEFAULT := 40.0
const BENCHMARK_RENDER_SCALE_START := 0.80
const MIN_RENDER_SCALE := 0.65
const MAX_RENDER_SCALE := 1.0

var _world_view: SubViewport
var _world_root: Node3D
var _bike_root: Node3D
var _player_camera: Camera3D
var _traffic_bodies: MultiMeshInstance3D
var _traffic_canopies: MultiMeshInstance3D
var _traffic_states: Array = []
var _mirror_slots: Array = []
var _lens_texture_rect: TextureRect
var _lens_material: ShaderMaterial
var _hud_panel: Panel
var _hud_label: Label

var _bike_speed_mps := 0.0
var _cockpit_view := false
var _mirrors_enabled := true
var _lens_mode := 2
var _lens_strength := 0.92
var _output_resolution := Vector2i(1280, 720)
var _render_scale := 1.0
var _frame_counter := 0
var _hud_refresh_elapsed := 0.0

var _benchmark_running := false
var _benchmark_warming_up := false
var _benchmark_auto_exit := false
var _benchmark_requested := false
var _test_requested := false
var _smoke_requested := false
var _benchmark_duration := BENCHMARK_SECONDS_DEFAULT
var _warmup_elapsed := 0.0
var _measurement_elapsed := 0.0
var _resolution_adjust_elapsed := 0.0
var _benchmark_samples: Array[float] = []
var _benchmark_scale_samples: Array[float] = []
var _previous_render_scale := 1.0

var _road_material: StandardMaterial3D
var _ground_material: StandardMaterial3D
var _marking_material: StandardMaterial3D
var _building_material: StandardMaterial3D
var _bike_paint: StandardMaterial3D
var _bike_rubber: StandardMaterial3D
var _bike_metal: StandardMaterial3D
var _bike_glass: StandardMaterial3D
var _traffic_material: StandardMaterial3D


func _ready() -> void:
	_parse_command_line()
	_create_world_view()
	_build_materials()
	_build_world()
	_build_bike()
	_build_traffic()
	_build_player_camera()
	_build_mirrors()
	_build_lens_output()
	_build_hud()
	_update_lens_parameters()
	_refresh_hud()
	if _test_requested:
		call_deferred("_run_tests")
	elif _smoke_requested:
		call_deferred("_run_smoke_test")
	elif _benchmark_requested:
		call_deferred("_start_auto_benchmark")


func _process(delta: float) -> void:
	_frame_counter += 1
	_update_bike(delta)
	_update_traffic(delta)
	_update_benchmark(delta)
	_update_camera(delta)
	_update_mirrors()
	_hud_refresh_elapsed += delta
	if _hud_refresh_elapsed >= 0.15:
		_hud_refresh_elapsed = 0.0
		_refresh_hud()


func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	match event.keycode:
		KEY_C:
			if not _benchmark_running:
				_cockpit_view = not _cockpit_view
				_snap_camera()
		KEY_M:
			if not _benchmark_running:
				_set_mirrors_enabled(not _mirrors_enabled)
		KEY_F1:
			if not _benchmark_running:
				_cycle_lens_mode()
		KEY_H:
			_hud_panel.visible = not _hud_panel.visible
		KEY_F2:
			if not _benchmark_running:
				_start_benchmark(false)


func _parse_command_line() -> void:
	var args := OS.get_cmdline_user_args()
	for index in range(args.size()):
		var arg: String = args[index]
		if arg == "--test":
			_test_requested = true
		elif arg == "--smoke":
			_smoke_requested = true
		elif arg == "--benchmark":
			_benchmark_requested = true
			_benchmark_auto_exit = true
		elif arg == "--benchmark-seconds" and index + 1 < args.size():
			_benchmark_duration = clampf(args[index + 1].to_float(), 10.0, 180.0)
		elif arg.begins_with("--benchmark-seconds="):
			_benchmark_duration = clampf(arg.get_slice("=", 1).to_float(), 10.0, 180.0)


func _create_world_view() -> void:
	_world_view = SubViewport.new()
	_world_view.name = "Phase0WorldViewport"
	_world_view.size = _output_resolution
	_world_view.own_world_3d = true
	_world_view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_world_view.gui_disable_input = true
	add_child(_world_view)
	_world_root = Node3D.new()
	_world_root.name = "Phase0World"
	_world_view.add_child(_world_root)


func _build_materials() -> void:
	_road_material = _make_material(Color(0.075, 0.085, 0.095), 0.95)
	_ground_material = _make_material(Color(0.16, 0.23, 0.18), 1.0)
	_marking_material = _make_material(Color(0.88, 0.84, 0.68), 0.9)
	_building_material = _make_material(Color(0.37, 0.40, 0.42), 0.92)
	_bike_paint = _make_material(Color(0.03, 0.23, 0.32), 0.38, 0.45)
	_bike_rubber = _make_material(Color(0.025, 0.03, 0.035), 1.0)
	_bike_metal = _make_material(Color(0.28, 0.31, 0.33), 0.35, 0.65)
	_bike_glass = _make_material(Color(0.10, 0.36, 0.43), 0.2, 0.2)
	_traffic_material = _make_material(Color(0.27, 0.37, 0.43), 0.72, 0.1)


func _make_material(
	color: Color, roughness: float = 0.8, metallic: float = 0.0
) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = roughness
	material.metallic = metallic
	return material


func _build_world() -> void:
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.46, 0.62, 0.72)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.72, 0.78, 0.84)
	environment.ambient_light_energy = 0.72
	var world_environment := WorldEnvironment.new()
	world_environment.environment = environment
	_world_root.add_child(world_environment)

	var sun := DirectionalLight3D.new()
	sun.name = "Phase 0 clear-weather sun"
	sun.rotation_degrees = Vector3(-48.0, -28.0, 0.0)
	sun.light_energy = 1.05
	sun.shadow_enabled = false
	_world_root.add_child(sun)

	_add_box(
		_world_root,
		"Ground / test terrain",
		Vector3(220.0, 0.18, 1200.0),
		Vector3(0.0, -0.17, 0.0),
		_ground_material
	)
	_add_box(
		_world_root,
		"Road / 1 km strip",
		Vector3(14.0, 0.16, ROAD_LENGTH),
		Vector3(0.0, -0.015, 0.0),
		_road_material
	)
	_build_road_markings()
	_build_roadside_fixtures()


func _add_box(
	parent: Node3D, object_name: String, size: Vector3, position: Vector3, material: Material
) -> MeshInstance3D:
	var mesh := BoxMesh.new()
	mesh.size = size
	var instance := MeshInstance3D.new()
	instance.name = object_name
	instance.mesh = mesh
	instance.material_override = material
	instance.position = position
	instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(instance)
	return instance


func _build_road_markings() -> void:
	_add_box(
		_world_root,
		"Road edge marking left",
		Vector3(0.12, 0.012, ROAD_LENGTH),
		Vector3(-6.25, 0.071, 0.0),
		_marking_material
	)
	_add_box(
		_world_root,
		"Road edge marking right",
		Vector3(0.12, 0.012, ROAD_LENGTH),
		Vector3(6.13, 0.071, 0.0),
		_marking_material
	)
	var dash_mesh := BoxMesh.new()
	dash_mesh.size = Vector3(0.15, 0.012, 6.0)
	var dash_count := 71
	var dash_multimesh := _make_multimesh(
		dash_mesh, _marking_material, dash_count, "Dashed center line"
	)
	for index in range(dash_count):
		var z := -496.0 + float(index) * 14.0
		dash_multimesh.multimesh.set_instance_transform(
			index, Transform3D(Basis.IDENTITY, Vector3(0.0, 0.071, z))
		)


func _build_roadside_fixtures() -> void:
	for index in range(8):
		var z := -430.0 + float(index) * 120.0
		var left_height := 6.0 + float(index % 3) * 2.0
		var right_height := 7.0 + float((index + 1) % 4) * 1.5
		_add_box(
			_world_root,
			"Fixture building L %02d" % index,
			Vector3(10.0, left_height, 22.0),
			Vector3(-17.0, left_height * 0.5, z),
			_building_material
		)
		_add_box(
			_world_root,
			"Fixture building R %02d" % index,
			Vector3(11.0, right_height, 26.0),
			Vector3(17.0, right_height * 0.5, z + 34.0),
			_building_material
		)

	var pole_mesh := CylinderMesh.new()
	pole_mesh.top_radius = 0.06
	pole_mesh.bottom_radius = 0.09
	pole_mesh.height = 6.2
	var pole_count := 16
	var poles := _make_multimesh(pole_mesh, _bike_metal, pole_count, "Roadside light poles")
	for index in range(8):
		var z := -440.0 + float(index) * 130.0
		poles.multimesh.set_instance_transform(
			index * 2, Transform3D(Basis.IDENTITY, Vector3(-8.4, 3.1, z))
		)
		poles.multimesh.set_instance_transform(
			index * 2 + 1, Transform3D(Basis.IDENTITY, Vector3(8.4, 3.1, z + 58.0))
		)


func _make_multimesh(
	mesh: Mesh, material: Material, count: int, object_name: String
) -> MultiMeshInstance3D:
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	multi.mesh = mesh
	multi.instance_count = count
	var instance := MultiMeshInstance3D.new()
	instance.name = object_name
	instance.multimesh = multi
	instance.material_override = material
	_world_root.add_child(instance)
	return instance


func _build_bike() -> void:
	_bike_root = Node3D.new()
	_bike_root.name = "Bike / Phase 0 procedural fixture (not production art)"
	_bike_root.position = Vector3(0.0, 0.02, BENCHMARK_START_Z)
	_world_root.add_child(_bike_root)
	_add_bike_box("Lower frame", Vector3(0.28, 0.28, 1.24), Vector3(0.0, 0.76, -0.03), _bike_metal)
	_add_bike_box("Engine block", Vector3(0.58, 0.5, 0.62), Vector3(0.0, 0.76, 0.03), _bike_metal)
	_add_bike_box("Fuel tank", Vector3(0.5, 0.34, 0.72), Vector3(0.0, 1.11, -0.1), _bike_paint)
	_add_bike_box("Front fairing", Vector3(0.48, 0.38, 0.56), Vector3(0.0, 1.08, 0.63), _bike_paint)
	_add_bike_box("Tail fairing", Vector3(0.42, 0.26, 0.58), Vector3(0.0, 1.04, -0.7), _bike_paint)
	_add_bike_box("Seat", Vector3(0.42, 0.12, 0.66), Vector3(0.0, 1.22, -0.36), _bike_rubber)
	_add_bike_box(
		"Windshield fixture", Vector3(0.46, 0.36, 0.045), Vector3(0.0, 1.47, 0.89), _bike_glass
	)
	_add_bike_box("Handlebar", Vector3(0.74, 0.045, 0.045), Vector3(0.0, 1.22, 0.52), _bike_metal)
	_add_bike_box(
		"Dashboard fixture", Vector3(0.3, 0.16, 0.18), Vector3(0.0, 1.28, 0.67), _bike_metal
	)
	_add_bike_box(
		"Front lamp fixture", Vector3(0.3, 0.12, 0.06), Vector3(0.0, 0.94, 0.94), _bike_glass
	)
	_add_bike_wheel("Rear wheel", Vector3(0.0, 0.39, -0.97))
	_add_bike_wheel("Front wheel", Vector3(0.0, 0.39, 1.06))


func _add_bike_box(
	object_name: String, size: Vector3, position: Vector3, material: Material
) -> void:
	var mesh := BoxMesh.new()
	mesh.size = size
	var part := MeshInstance3D.new()
	part.name = "Fixture part / " + object_name
	part.mesh = mesh
	part.material_override = material
	part.position = position
	part.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_bike_root.add_child(part)


func _add_bike_wheel(object_name: String, position: Vector3) -> void:
	var mesh := CylinderMesh.new()
	mesh.top_radius = 0.39
	mesh.bottom_radius = 0.39
	mesh.height = 0.15
	mesh.radial_segments = 12
	var wheel := MeshInstance3D.new()
	wheel.name = "Fixture part / " + object_name
	wheel.mesh = mesh
	wheel.material_override = _bike_rubber
	wheel.position = position
	wheel.rotation.z = PI * 0.5
	wheel.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_bike_root.add_child(wheel)


func _build_traffic() -> void:
	var body_mesh := BoxMesh.new()
	body_mesh.size = Vector3(1.85, 0.72, 3.8)
	_traffic_bodies = _make_multimesh(
		body_mesh, _traffic_material, TRAFFIC_COUNT, "Traffic / 50 pooled bodies"
	)
	var canopy_mesh := BoxMesh.new()
	canopy_mesh.size = Vector3(1.38, 0.62, 1.75)
	_traffic_canopies = _make_multimesh(
		canopy_mesh, _traffic_material, TRAFFIC_COUNT, "Traffic / 50 pooled cabins"
	)
	var rng := RandomNumberGenerator.new()
	rng.seed = TRAFFIC_SEED
	for index in range(TRAFFIC_COUNT):
		var state := {
			"initial_distance":
			fposmod(
				float(index) * ROAD_LENGTH / float(TRAFFIC_COUNT) + rng.randf_range(0.0, 4.0),
				ROAD_LENGTH
			),
			"distance": 0.0,
			"lane_x": -2.45 if index % 2 == 0 else 2.45,
			"speed": rng.randf_range(14.0, 23.0)
		}
		state["distance"] = state["initial_distance"]
		_traffic_states.append(state)
	_reset_traffic()


func _reset_traffic() -> void:
	for index in range(_traffic_states.size()):
		var state: Dictionary = _traffic_states[index]
		state["distance"] = state["initial_distance"]
		_traffic_states[index] = state
		_set_traffic_transform(index, state)


func _update_traffic(delta: float) -> void:
	for index in range(_traffic_states.size()):
		var state: Dictionary = _traffic_states[index]
		state["distance"] = fposmod(
			float(state["distance"]) + float(state["speed"]) * delta, ROAD_LENGTH
		)
		_traffic_states[index] = state
		_set_traffic_transform(index, state)


func _set_traffic_transform(index: int, state: Dictionary) -> void:
	var position := Vector3(
		float(state["lane_x"]), 0.4, float(state["distance"]) - ROAD_LENGTH * 0.5
	)
	_traffic_bodies.multimesh.set_instance_transform(index, Transform3D(Basis.IDENTITY, position))
	_traffic_canopies.multimesh.set_instance_transform(
		index, Transform3D(Basis.IDENTITY, position + Vector3(0.0, 0.48, -0.22))
	)


func _build_player_camera() -> void:
	_player_camera = Camera3D.new()
	_player_camera.name = "Player Camera / Phase 0 lens camera"
	_player_camera.fov = 84.0
	_player_camera.near = 0.06
	_player_camera.far = 1400.0
	_player_camera.current = true
	_world_root.add_child(_player_camera)
	_snap_camera()


func _build_mirrors() -> void:
	for index in range(2):
		var side := -1.0 if index == 0 else 1.0
		var side_name := "Left" if index == 0 else "Right"
		var mirror_view := SubViewport.new()
		mirror_view.name = "%s mirror render target" % side_name
		mirror_view.size = Vector2i(256, 128)
		mirror_view.own_world_3d = false
		mirror_view.world_3d = _world_view.world_3d
		mirror_view.render_target_update_mode = SubViewport.UPDATE_DISABLED
		mirror_view.gui_disable_input = true
		add_child(mirror_view)
		var mirror_camera := Camera3D.new()
		mirror_camera.name = "%s mirror camera" % side_name
		mirror_camera.fov = 78.0
		mirror_camera.near = 0.1
		mirror_camera.far = 180.0
		mirror_camera.cull_mask = 1
		mirror_camera.current = true
		mirror_view.add_child(mirror_camera)

		var surface := MeshInstance3D.new()
		surface.name = "%s mirror display" % side_name
		var quad := QuadMesh.new()
		quad.size = Vector2(0.34, 0.17)
		surface.mesh = quad
		surface.position = Vector3(side * 0.52, 1.55, 0.88)
		surface.rotation.y = deg_to_rad(side * 7.0)
		surface.layers = 2
		surface.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		var mirror_material := StandardMaterial3D.new()
		mirror_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		mirror_material.cull_mode = BaseMaterial3D.CULL_DISABLED
		mirror_material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR
		mirror_material.albedo_texture = mirror_view.get_texture()
		surface.material_override = mirror_material
		_bike_root.add_child(surface)
		_mirror_slots.append({"viewport": mirror_view, "camera": mirror_camera, "surface": surface})


func _build_lens_output() -> void:
	_lens_texture_rect = TextureRect.new()
	_lens_texture_rect.name = "Horizontal fisheye output"
	_lens_texture_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_lens_texture_rect.texture = _world_view.get_texture()
	_lens_texture_rect.stretch_mode = TextureRect.STRETCH_SCALE
	_lens_texture_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_lens_material = ShaderMaterial.new()
	_lens_material.shader = FishEyeShader
	_lens_texture_rect.material = _lens_material
	add_child(_lens_texture_rect)


func _build_hud() -> void:
	_hud_panel = Panel.new()
	_hud_panel.name = "Phase 0 debug and benchmark panel"
	_hud_panel.position = Vector2(14.0, 14.0)
	_hud_panel.size = Vector2(540.0, 190.0)
	var panel_style := StyleBoxFlat.new()
	panel_style.bg_color = Color(0.015, 0.025, 0.035, 0.86)
	panel_style.set_content_margin_all(12.0)
	_hud_panel.add_theme_stylebox_override("panel", panel_style)
	_hud_label = Label.new()
	_hud_label.position = Vector2(10.0, 8.0)
	_hud_label.size = Vector2(520.0, 170.0)
	_hud_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_hud_label.add_theme_color_override("font_color", Color(0.93, 0.96, 0.99))
	_hud_panel.add_child(_hud_label)
	add_child(_hud_panel)


func _update_bike(delta: float) -> void:
	if _benchmark_running:
		return
	var throttle := 0.0
	if Input.is_key_pressed(KEY_W):
		throttle += 1.0
	if Input.is_key_pressed(KEY_S):
		throttle -= 1.0
	var target_speed := 0.0
	if throttle > 0.0:
		target_speed = 105.0 / 3.6
	elif throttle < 0.0:
		target_speed = 0.0
	var acceleration := 8.0 if throttle > 0.0 else 28.0 if throttle < 0.0 else 1.8
	_bike_speed_mps = move_toward(_bike_speed_mps, target_speed, acceleration * delta)
	var steer := 0.0
	if Input.is_key_pressed(KEY_D):
		steer += 1.0
	if Input.is_key_pressed(KEY_A):
		steer -= 1.0
	var lateral_rate := lerpf(0.0, 2.2, clampf(_bike_speed_mps / 16.0, 0.0, 1.0))
	_bike_root.position.x = clampf(_bike_root.position.x + steer * lateral_rate * delta, -4.2, 4.2)
	_bike_root.position.z += _bike_speed_mps * delta
	_bike_root.rotation.y = lerp_angle(
		_bike_root.rotation.y, steer * deg_to_rad(4.0), 1.0 - exp(-5.0 * delta)
	)
	_bike_root.rotation.z = lerpf(
		_bike_root.rotation.z, -steer * deg_to_rad(6.0), 1.0 - exp(-5.0 * delta)
	)


func _update_camera(delta: float) -> void:
	if _benchmark_running:
		return
	if _cockpit_view:
		var cockpit_position: Vector3 = (
			_bike_root.global_position + _bike_root.global_basis * Vector3(0.0, 1.43, 0.18)
		)
		_player_camera.global_position = cockpit_position
		var forward := Vector3(sin(_bike_root.rotation.y), 0.0, cos(_bike_root.rotation.y))
		_player_camera.look_at(cockpit_position + forward * 20.0 + Vector3.UP * 0.5, Vector3.UP)
		return
	var forward := Vector3(sin(_bike_root.rotation.y), 0.0, cos(_bike_root.rotation.y))
	var target_position: Vector3 = _bike_root.global_position - forward * 7.2 + Vector3.UP * 2.65
	var look_point: Vector3 = _bike_root.global_position + forward * 9.0 + Vector3.UP * 1.15
	_player_camera.global_position = _player_camera.global_position.lerp(
		target_position, 1.0 - exp(-5.0 * delta)
	)
	_player_camera.look_at(look_point, Vector3.UP)
	_update_lens_parameters()


func _snap_camera() -> void:
	if not is_instance_valid(_player_camera) or not is_instance_valid(_bike_root):
		return
	if _cockpit_view:
		var cockpit_position: Vector3 = (
			_bike_root.global_position + _bike_root.global_basis * Vector3(0.0, 1.43, 0.18)
		)
		_player_camera.global_position = cockpit_position
		var cockpit_forward := Vector3(sin(_bike_root.rotation.y), 0.0, cos(_bike_root.rotation.y))
		_player_camera.look_at(cockpit_position + cockpit_forward * 20.0, Vector3.UP)
	else:
		var forward := Vector3(sin(_bike_root.rotation.y), 0.0, cos(_bike_root.rotation.y))
		var camera_position: Vector3 = (
			_bike_root.global_position - forward * 7.2 + Vector3.UP * 2.65
		)
		var look_point: Vector3 = _bike_root.global_position + forward * 9.0 + Vector3.UP * 1.15
		_player_camera.global_position = camera_position
		_player_camera.look_at(look_point, Vector3.UP)
	_update_lens_parameters()


func _build_mirror_camera_state(slot: Dictionary) -> void:
	var mirror_camera: Camera3D = slot["camera"]
	var surface: MeshInstance3D = slot["surface"]
	var normal := surface.global_basis.z.normalized()
	var surface_position := surface.global_position
	var eye_transform := _player_camera.global_transform
	var reflected_position := (
		surface_position + _reflect_vector(eye_transform.origin - surface_position, normal)
	)
	var reflected_forward := _reflect_vector(-eye_transform.basis.z, normal).normalized()
	var reflected_up := _reflect_vector(eye_transform.basis.y, normal).normalized()
	var reflected_right := -_reflect_vector(eye_transform.basis.x, normal).normalized()
	if reflected_forward.length_squared() < 0.001 or reflected_up.length_squared() < 0.001:
		return
	var reflected_basis := (
		Basis(reflected_right, reflected_up, -reflected_forward).orthonormalized()
	)
	mirror_camera.global_transform = Transform3D(reflected_basis, reflected_position)


func _reflect_vector(value: Vector3, normal: Vector3) -> Vector3:
	return value - 2.0 * value.dot(normal) * normal


func _update_mirrors() -> void:
	if not _mirrors_enabled or _mirror_slots.is_empty() or _frame_counter % 2 != 0:
		return
	for slot_value in _mirror_slots:
		var slot: Dictionary = slot_value
		_build_mirror_camera_state(slot)
		var mirror_view: SubViewport = slot["viewport"]
		mirror_view.render_target_update_mode = SubViewport.UPDATE_ONCE


func _set_mirrors_enabled(enabled: bool) -> void:
	_mirrors_enabled = enabled
	for slot_value in _mirror_slots:
		var slot: Dictionary = slot_value
		var surface: MeshInstance3D = slot["surface"]
		var mirror_view: SubViewport = slot["viewport"]
		surface.visible = enabled
		mirror_view.render_target_update_mode = (
			SubViewport.UPDATE_ONCE if enabled else SubViewport.UPDATE_DISABLED
		)


func _cycle_lens_mode() -> void:
	_lens_mode = (_lens_mode + 1) % 3
	match _lens_mode:
		0:
			_lens_strength = 0.0
		1:
			_lens_strength = 0.55
		2:
			_lens_strength = 0.92
	_update_lens_parameters()


func _update_lens_parameters() -> void:
	if (
		not is_instance_valid(_lens_material)
		or not is_instance_valid(_player_camera)
		or not is_instance_valid(_world_view)
	):
		return
	var aspect := float(_world_view.size.x) / maxf(1.0, float(_world_view.size.y))
	var source_half_tan := tan(deg_to_rad(_player_camera.fov * 0.5)) * aspect
	_lens_material.set_shader_parameter("distortion_strength", _lens_strength)
	_lens_material.set_shader_parameter("edge_coverage", 0.93)
	_lens_material.set_shader_parameter("source_half_tan", source_half_tan)
	_lens_material.set_shader_parameter("vertical_scale", 1.0)


func _start_auto_benchmark() -> void:
	_start_benchmark(true)


func _start_benchmark(auto_exit: bool) -> void:
	if _benchmark_running:
		return
	_benchmark_auto_exit = auto_exit
	_benchmark_running = true
	_benchmark_warming_up = true
	_warmup_elapsed = 0.0
	_measurement_elapsed = 0.0
	_resolution_adjust_elapsed = 0.0
	_benchmark_samples.clear()
	_benchmark_scale_samples.clear()
	_previous_render_scale = _render_scale
	_bike_speed_mps = BENCHMARK_SPEED_KPH / 3.6
	_bike_root.rotation = Vector3.ZERO
	_bike_root.position = Vector3(0.0, 0.02, BENCHMARK_START_Z)
	_cockpit_view = false
	_lens_mode = 2
	_lens_strength = 0.92
	_set_mirrors_enabled(true)
	_reset_traffic()
	if not _is_headless():
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
		DisplayServer.window_set_size(Vector2i(1920, 1080))
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	_output_resolution = Vector2i(1920, 1080)
	_render_scale = BENCHMARK_RENDER_SCALE_START
	_apply_render_scale()
	_snap_camera()
	_refresh_hud()
	print(
		(
			"OPEN ROAD benchmark: %dx%d output request, 80 km/h, 50 vehicles, seed %d."
			% [_output_resolution.x, _output_resolution.y, TRAFFIC_SEED]
		)
	)


func _is_headless() -> bool:
	return DisplayServer.get_name().to_lower() == "headless"


func _begin_measurement() -> void:
	_benchmark_warming_up = false
	_measurement_elapsed = 0.0
	_resolution_adjust_elapsed = 0.0
	_benchmark_samples.clear()
	_benchmark_scale_samples.clear()
	_reset_traffic()
	_bike_root.position = Vector3(0.0, 0.02, BENCHMARK_START_Z)
	_bike_root.rotation = Vector3.ZERO
	_bike_speed_mps = BENCHMARK_SPEED_KPH / 3.6
	_snap_camera()
	_refresh_hud()
	print(
		(
			"OPEN ROAD measured interval started: %d seconds, fixed 80 km/h, 50 traffic fixtures."
			% int(_benchmark_duration)
		)
	)


func _update_benchmark(delta: float) -> void:
	if not _benchmark_running:
		return
	if _benchmark_warming_up:
		_warmup_elapsed += delta
		_bike_root.position = Vector3(
			0.0, 0.02, BENCHMARK_START_Z + BENCHMARK_SPEED_KPH / 3.6 * _warmup_elapsed
		)
		if _warmup_elapsed >= BENCHMARK_WARMUP_SECONDS:
			_begin_measurement()
		return
	_measurement_elapsed += delta
	_bike_root.position = Vector3(
		0.0, 0.02, BENCHMARK_START_Z + BENCHMARK_SPEED_KPH / 3.6 * _measurement_elapsed
	)
	var frame_time_ms := maxf(delta * 1000.0, 0.001)
	_benchmark_samples.append(frame_time_ms)
	_benchmark_scale_samples.append(_render_scale)
	_resolution_adjust_elapsed += delta
	if _resolution_adjust_elapsed >= 0.5:
		_adjust_render_scale(frame_time_ms)
	if _measurement_elapsed >= _benchmark_duration:
		_finish_benchmark()


func _adjust_render_scale(last_frame_ms: float) -> void:
	_resolution_adjust_elapsed = 0.0
	if last_frame_ms > 25.5:
		_render_scale = maxf(MIN_RENDER_SCALE, _render_scale - 0.04)
	elif last_frame_ms < 20.5:
		_render_scale = minf(MAX_RENDER_SCALE, _render_scale + 0.02)
	_apply_render_scale()


func _apply_render_scale() -> void:
	if not is_instance_valid(_world_view):
		return
	_world_view.size = Vector2i(
		maxi(1, roundi(float(_output_resolution.x) * _render_scale)),
		maxi(1, roundi(float(_output_resolution.y) * _render_scale))
	)
	_update_lens_parameters()


func _finish_benchmark() -> void:
	_benchmark_running = false
	_benchmark_warming_up = false
	_bike_speed_mps = 0.0
	var output_directory := OS.get_user_data_dir().path_join("Benchmark")
	DirAccess.make_dir_recursive_absolute(output_directory)
	var stamp := str(int(Time.get_unix_time_from_system()))
	var csv_path := output_directory.path_join("benchmark_%s.csv" % stamp)
	var summary_path := output_directory.path_join("benchmark_summary.txt")
	var frame_file := FileAccess.open(csv_path, FileAccess.WRITE)
	if frame_file == null:
		push_error("Could not write benchmark CSV: %s" % error_string(FileAccess.get_open_error()))
		return
	frame_file.store_line(
		"frame_index,elapsed_seconds,frame_time_ms,fps,render_scale,traffic_count"
	)
	var frame_total := 0.0
	var histogram: Array[int] = [0, 0, 0, 0, 0, 0, 0]
	for index in range(_benchmark_samples.size()):
		var frame_ms: float = _benchmark_samples[index]
		frame_total += frame_ms
		histogram[BenchmarkMath.histogram_bucket(frame_ms)] += 1
		frame_file.store_line(
			(
				"%d,%.4f,%.3f,%.2f,%.3f,%d"
				% [
					index,
					(
						float(index)
						/ maxf(1.0, float(_benchmark_samples.size()))
						* _measurement_elapsed
					),
					frame_ms,
					BenchmarkMath.fps_for_frame_time(frame_ms),
					_benchmark_scale_samples[index],
					TRAFFIC_COUNT
				]
			)
		)
	frame_file.close()
	var average_fps := float(_benchmark_samples.size()) / maxf(0.001, _measurement_elapsed)
	var average_frame_ms := frame_total / maxf(1.0, float(_benchmark_samples.size()))
	var p99_ms := BenchmarkMath.percentile(_benchmark_samples, 0.99)
	var p999_ms := BenchmarkMath.percentile(_benchmark_samples, 0.999)
	var one_percent_low := BenchmarkMath.fps_for_frame_time(p99_ms)
	var point_one_percent_low := BenchmarkMath.fps_for_frame_time(p999_ms)
	var scale_average := 0.0
	for scale_value in _benchmark_scale_samples:
		scale_average += scale_value
	scale_average /= maxf(1.0, float(_benchmark_scale_samples.size()))
	var pass_text := (
		"PASS for this run"
		if average_fps >= 40.0 and one_percent_low >= 30.0
		else "NOT MET for this run"
	)
	var summary := "OPEN ROAD - PHASE 0 BENCHMARK\n"
	summary += "==============================\n"
	summary += "Timestamp UTC: %s\n" % Time.get_datetime_string_from_system(true)
	summary += "Engine: Godot %s\n" % Engine.get_version_info().string
	summary += "Route: 1 km straight test road; 80 km/h fixed speed\n"
	summary += "Traffic: seed %d, %d vehicles\n" % [TRAFFIC_SEED, TRAFFIC_COUNT]
	summary += "Output request: 1920x1080 windowed\n"
	summary += (
		"Horizontal lens: strength %.2f; mirrors: %s\n"
		% [_lens_strength, "on" if _mirrors_enabled else "off"]
	)
	summary += (
		"Render scale min/average/max: %.3f / %.3f / %.3f\n"
		% [
			_min_array(_benchmark_scale_samples),
			scale_average,
			_max_array(_benchmark_scale_samples)
		]
	)
	summary += (
		"Frames: %d; measured seconds: %.3f\n" % [_benchmark_samples.size(), _measurement_elapsed]
	)
	summary += "Average FPS: %.2f\n" % average_fps
	summary += "Average frame time: %.2f ms\n" % average_frame_ms
	var histogram_text := (
		"%d, %d, %d, %d, %d, %d, %d"
		% [
			histogram[0],
			histogram[1],
			histogram[2],
			histogram[3],
			histogram[4],
			histogram[5],
			histogram[6]
		]
	)
	var histogram_line := (
		"Frame-time histogram (counts; buckets <8.33 / <12.5 / <16.67 / <22 / "
		+ "<33.33 / <50 / >=50 ms): "
		+ histogram_text
		+ "\n"
	)
	summary += histogram_line
	summary += "1%% low FPS (p99): %.2f (%.2f ms)\n" % [one_percent_low, p99_ms]
	summary += "0.1%% low FPS (p99.9): %.2f (%.2f ms)\n" % [point_one_percent_low, p999_ms]
	summary += "40 average / 30 1%% low threshold: %s\n" % pass_text
	summary += "CPU/GPU frame timing and VRAM are not reported by this Phase 0 build.\n"
	summary += "This is one run, not performance acceptance. Owner target-PC review is required.\n"
	summary += "CSV: %s\n" % csv_path
	var summary_file := FileAccess.open(summary_path, FileAccess.WRITE)
	if summary_file == null:
		push_error(
			"Could not write benchmark summary: %s" % error_string(FileAccess.get_open_error())
		)
		return
	summary_file.store_string(summary)
	summary_file.close()
	print(summary)
	_render_scale = _previous_render_scale
	_output_resolution = Vector2i(1280, 720)
	_apply_render_scale()
	if not _is_headless():
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED)
		DisplayServer.window_set_size(_output_resolution)
	_refresh_hud()
	if _benchmark_auto_exit:
		get_tree().quit(0)


func _min_array(values: Array[float]) -> float:
	if values.is_empty():
		return 0.0
	var result := values[0]
	for value in values:
		result = minf(result, value)
	return result


func _max_array(values: Array[float]) -> float:
	if values.is_empty():
		return 0.0
	var result := values[0]
	for value in values:
		result = maxf(result, value)
	return result


func _refresh_hud() -> void:
	if not is_instance_valid(_hud_label):
		return
	var mode_text := "Cockpit" if _cockpit_view else "Chase"
	var status_text := (
		"WARM-UP %.1f/%.0f s" % [_warmup_elapsed, BENCHMARK_WARMUP_SECONDS]
		if _benchmark_running and _benchmark_warming_up
		else (
			"MEASURING %.1f/%.0f s" % [_measurement_elapsed, _benchmark_duration]
			if _benchmark_running
			else "READY"
		)
	)
	var fps := Engine.get_frames_per_second()
	var text := "OPEN ROAD  /  PHASE 0 GODOT SPIKE\n"
	text += "%s  |  FPS %d  |  Render scale %.2f\n" % [status_text, fps, _render_scale]
	text += (
		"Bike %.1f km/h  |  Traffic %d/%d  |  Camera %s\n"
		% [_bike_speed_mps * 3.6, _traffic_states.size(), TRAFFIC_COUNT, mode_text]
	)
	text += (
		"Horizontal lens %.2f  |  Mirrors %s\n"
		% [_lens_strength, "on" if _mirrors_enabled else "off"]
	)
	text += "W/S throttle/brake  A/D steer  C camera  M mirrors  F1 lens\n"
	text += "F2 benchmark  H hide panel  |  test geometry, not release art"
	_hud_label.text = text


func _run_tests() -> void:
	var failures: Array[String] = Phase0Tests.run()
	if _traffic_states.size() != TRAFFIC_COUNT:
		failures.append("traffic fixture must initialize exactly 50 vehicles")
	if _mirror_slots.size() != 2:
		failures.append("two mirror viewports must be present")
	if (
		not is_instance_valid(_bike_root)
		or not is_instance_valid(_player_camera)
		or not is_instance_valid(_world_view)
		or not is_instance_valid(_lens_texture_rect)
		or not is_instance_valid(_lens_material)
	):
		failures.append("world, bike, camera, or fisheye output must initialize")
	if failures.is_empty():
		print("OPENROAD_TESTS_OK: math, lens mapping, traffic pool, cameras, and startup objects.")
		get_tree().quit(0)
		return
	for failure in failures:
		push_error("OPENROAD_TEST_FAIL: " + failure)
	get_tree().quit(1)


func _run_smoke_test() -> void:
	await get_tree().process_frame
	await get_tree().process_frame
	var passed := (
		_traffic_states.size() == TRAFFIC_COUNT
		and _mirror_slots.size() == 2
		and is_instance_valid(_bike_root)
		and is_instance_valid(_player_camera)
		and is_instance_valid(_world_view)
		and is_instance_valid(_lens_texture_rect)
		and is_instance_valid(_lens_material)
	)
	if passed:
		print(
			"OPENROAD_SMOKE_OK: scene, bike, 50 traffic vehicles, two mirrors, and fisheye initialized."
		)
	else:
		push_error(
			"OPENROAD_SMOKE_FAIL: world, bike, camera, traffic, mirrors, or lens output missing."
		)
	get_tree().quit(0 if passed else 1)
