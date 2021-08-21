﻿using Sandbox;
using System;

//[Library( "entity_vehicle", Title = "Vehicle", Spawnable = true )]
public partial class Vehicle_base : Prop, IUse
{
	[ConVar.Replicated( "debug_vehicle" )]
	public static bool debug_vehicle { get; set; } = false;

	[ConVar.Replicated( "vehicle_accelspeed" )]
	public static float vehicle_accelspeed { get; set; } = 450.0f;

	private Array[] vehicleWheels;

	private Vehicle_wheels frontLeft;
	private Vehicle_wheels frontRight;
	private Vehicle_wheels backLeft;
	private Vehicle_wheels backRight;

	private float frontLeftDistance;
	private float frontRightDistance;
	private float backLeftDistance;
	private float backRightDistance;

	private bool frontWheelsOnGround;
	private bool backWheelsOnGround;
	private float accelerateDirection;
	private float airRoll;
	private float airTilt;
	private float grip;
	private TimeSince timeSinceDriverLeft;

	public int WheelCount { get; private set; }

	[Net] private float WheelSpeed { get; set; }
	[Net] private float TurnDirection { get; set; }
	[Net] private float AccelerationTilt { get; set; }
	[Net] private float TurnLean { get; set; }

	[Net] public float MovementSpeed { get; private set; }
	[Net] public bool Grounded { get; private set; }

	private struct InputState
	{
		public float throttle;
		public float turning;
		public float breaking;
		public float tilt;
		public float roll;

		public void Reset()
		{
			throttle = 0;
			turning = 0;
			breaking = 0;
			tilt = 0;
			roll = 0;
		}
	}

	private InputState currentInput;

	public Vehicle_base()
	{
		//frontLeft = new Vehicle_wheel( this );
		//frontRight = new Vehicle_wheel( this );
		//backLeft = new Vehicle_wheel( this );
		//backRight = new Vehicle_wheel( this );
	}

	[Net] public Player driver { get; private set; }
	public Array[] VehicleWheels { get => vehicleWheels; set => vehicleWheels = value; }

	private ModelEntity vehicle_steering_wheel_base;
	private ModelEntity vehicle_steering_wheel;
	private ModelEntity chassis_axle_rear;
	private ModelEntity chassis_axle_front;
	private ModelEntity wheel0_steering;
	private ModelEntity wheel1_steering;
	private ModelEntity wheel0;
	private ModelEntity wheel1;
	private ModelEntity wheel2;
	private ModelEntity wheel3;

	public override void Spawn()
	{
		base.Spawn();

		var modelName = "models/car/car.vmdl";
		SetModel( modelName );
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
		SetInteractsExclude( CollisionLayer.Player );
		EnableSelfCollisions = false;

		var trigger = new ModelEntity
		{
			Parent = this,
			Position = Position,
			Rotation = Rotation,
			EnableTouch = true,
			CollisionGroup = CollisionGroup.Trigger,
			Transmit = TransmitType.Never,
			EnableSelfCollisions = true,
		};

		trigger.SetModel( modelName );
		trigger.SetupPhysicsFromModel( PhysicsMotionType.Keyframed, false );
		trigger.Transform = new Transform( new Vector3( 0, 0, -4.5f ) );
		trigger.Scale = 1.05f;
	}

	public override void ClientSpawn()
	{
		base.ClientSpawn();
		var wheelModel = "models/citizen_props/wheel02.vmdl"; //"entities/modular_vehicle/wheel_a.vmdl"
		{
			var vehicle_fuel_tank = new ModelEntity();
			vehicle_fuel_tank.SetModel( "entities/modular_vehicle/vehicle_fuel_tank.vmdl" );
			vehicle_fuel_tank.Transform = Transform;
			vehicle_fuel_tank.Parent = this;
			vehicle_fuel_tank.LocalPosition = new Vector3( 0.75f, 0, 0 ) * 40.0f;
		}

		{
			vehicle_steering_wheel_base = new ModelEntity();
			vehicle_steering_wheel_base.SetModel( "models/light_arrow.vmdl" );
			vehicle_steering_wheel_base.Transform = Transform;
			vehicle_steering_wheel_base.Parent = this;
			vehicle_steering_wheel_base.LocalPosition = new Vector3( 20, 0, 40 );
			vehicle_steering_wheel_base.LocalRotation = Rotation.From( -65, 0, 0 );
			vehicle_steering_wheel_base.Scale = 0f;

			{
				vehicle_steering_wheel = new ModelEntity();
				vehicle_steering_wheel.SetModel( "models/citizen_props/hotdog01.vmdl" );
				vehicle_steering_wheel.SetParent( vehicle_steering_wheel_base, "Steering_Wheel", new Transform( Vector3.Zero, Rotation.From( 0, 0, 0 ) ) );
				vehicle_steering_wheel.Scale = 1f;
			}
		}

		{
			chassis_axle_front = new ModelEntity();
			chassis_axle_front.SetModel( "entities/modular_vehicle/chassis_axle_front.vmdl" );
			chassis_axle_front.Transform = Transform;
			chassis_axle_front.Parent = this;
			chassis_axle_front.LocalPosition = new Vector3( 1.05f, 0, 0.35f ) * 40.0f;

			{
				wheel0_steering = new ModelEntity();
				wheel0_steering.SetModel( "models/ball/ball.vmdl" ); //models/light_arrow.vmdl
				wheel0_steering.SetParent( chassis_axle_front, "Wheel_Steer_R", new Transform( Vector3.Zero, Rotation.From( 0, 180, 0 ) ) );
				wheel0_steering.RenderColorAndAlpha = new Color32( 255, 255, 255, 0 );
				wheel0_steering.LocalScale = 0.25f;
				//wheel0_steering.LocalPosition = new Vector3( -4f, 0f, 0f );
				{
					wheel0 = new ModelEntity();
					wheel0.SetModel( wheelModel );
					wheel0.SetParent( wheel0_steering, "Wheel_Front_R", new Transform( Vector3.Zero, Rotation.From( 0, 180, 0 ) ) );
					wheel0.LocalScale = 0.9f * 4f;
				}
			}

			{
				wheel1_steering = new ModelEntity();
				wheel1_steering.SetModel( "models/ball/ball.vmdl" ); //models/light_arrow.vmdl
				wheel1_steering.SetParent( chassis_axle_front, "Wheel_Steer_L", new Transform( Vector3.Zero, Rotation.From( 0, 0, 0 ) ) );
				wheel1_steering.RenderColorAndAlpha = new Color32( 255, 255, 255, 0 );
				wheel1_steering.Scale = 0.25f;
				//wheel1_steering.LocalPosition = new Vector3( 4f, 0f, 0f );
				{
					wheel1 = new ModelEntity();
					wheel1.SetModel( wheelModel );
					wheel1.SetParent( wheel1_steering, "Wheel_Front_L", new Transform( Vector3.Zero, Rotation.From( 0, 0, 0 ) ) );
					wheel1.LocalScale = 0.9f * 4f;
				}
			}

			{
				var chassis_steering = new ModelEntity();
				chassis_steering.SetModel( "entities/modular_vehicle/chassis_steering.vmdl" );
				chassis_steering.SetParent( chassis_axle_front, "Axle_front_Center", new Transform( Vector3.Zero, Rotation.From( -90, 180, 0 ) ) );
			}
		}

		{
			chassis_axle_rear = new ModelEntity();
			chassis_axle_rear.SetModel( "entities/modular_vehicle/chassis_axle_rear.vmdl" );
			chassis_axle_rear.Transform = Transform;
			chassis_axle_rear.Parent = this;
			chassis_axle_rear.LocalPosition = new Vector3( -1.05f, 0, 0.35f ) * 40.0f;

			{
				var chassis_transmission = new ModelEntity();
				chassis_transmission.SetModel( "entities/modular_vehicle/chassis_transmission.vmdl" );
				chassis_transmission.SetParent( chassis_axle_rear, "Axle_Rear_Center", new Transform( Vector3.Zero, Rotation.From( -90, 180, 0 ) ) );
			}

			{
				wheel2 = new ModelEntity();
				wheel2.SetModel( wheelModel );
				wheel2.SetParent( chassis_axle_rear, "Axle_Rear_Center", new Transform( Vector3.Left * (0.7f * 40), Rotation.From( 0, 90, 0 ) ) );
				wheel2.LocalScale = 0.9f;
				//wheel2.LocalPosition = Position;
			}

			{
				wheel3 = new ModelEntity();
				wheel3.SetModel( wheelModel );
				wheel3.SetParent( chassis_axle_rear, "Axle_Rear_Center", new Transform( Vector3.Right * (0.7f * 40), Rotation.From( 0, -90, 0 ) ) );
				wheel3.LocalScale = 0.9f;
				//wheel3.LocalPosition = Position;
			}
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( driver is SandboxPlayer player )
		{
			RemoveDriver( player );
		}
	}

	public void ResetInput()
	{
		currentInput.Reset();
	}

	[Event.Tick.Server]
	protected void Tick()
	{
		if ( driver is SandboxPlayer player )
		{
			if ( player.LifeState != LifeState.Alive || player.Vehicle != this )
			{
				RemoveDriver( player );
			}
		}
	}

	public override void Simulate( Client owner )
	{
		if ( owner == null ) return;
		if ( !IsServer ) return;

		using ( Prediction.Off() )
		{
			currentInput.Reset();

			if ( Input.Pressed( InputButton.Use ) )
			{
				if ( owner.Pawn is SandboxPlayer player && !player.IsUseDisabled() )
				{
					RemoveDriver( player );

					return;
				}
			}

			currentInput.throttle = (Input.Down( InputButton.Forward ) ? 1 : 0) + (Input.Down( InputButton.Back ) ? -1 : 0);
			currentInput.turning = (Input.Down( InputButton.Left ) ? 1 : 0) + (Input.Down( InputButton.Right ) ? -1 : 0);
			currentInput.breaking = (Input.Down( InputButton.Jump ) ? 1 : 0);
			currentInput.tilt = (Input.Down( InputButton.Run ) ? 1 : 0) + (Input.Down( InputButton.Duck ) ? -1 : 0);
			currentInput.roll = (Input.Down( InputButton.Left ) ? 1 : 0) + (Input.Down( InputButton.Right ) ? -1 : 0);
		}
	}

	[Event.Physics.PreStep]
	public void OnPrePhysicsStep()
	{
		if ( !IsServer )
			return;

		var selfBody = PhysicsBody;
		if ( !selfBody.IsValid() )
			return;

		var body = selfBody.SelfOrParent;
		if ( !body.IsValid() )
			return;

		var dt = Time.Delta;

		body.DragEnabled = false;

		var rotation = selfBody.Rotation;

		accelerateDirection = currentInput.throttle.Clamp( -1, 1 ) * (1.0f - currentInput.breaking);
		TurnDirection = TurnDirection.LerpTo( currentInput.turning.Clamp( -1, 1 ), 1.0f - MathF.Pow( 0.001f, dt ) );

		airRoll = airRoll.LerpTo( currentInput.roll.Clamp( -1, 1 ), 2.5f - MathF.Pow( 0.0001f, dt ) );
		airTilt = airTilt.LerpTo( currentInput.tilt.Clamp( -1, 1 ), 2.0f - MathF.Pow( 0.0001f, dt ) );

		float targetTilt = 0;
		float targetLean = 0;

		var localVelocity = rotation.Inverse * body.Velocity;

		if ( backWheelsOnGround || frontWheelsOnGround )
		{
			var forwardSpeed = MathF.Abs( localVelocity.x );
			var speedFraction = MathF.Min( forwardSpeed / 500.0f, 1 );

			targetTilt = accelerateDirection.Clamp( -49.0f, 49.0f );
			targetLean = speedFraction * TurnDirection;
		}

		AccelerationTilt = AccelerationTilt.LerpTo( targetTilt, 1.0f - MathF.Pow( 0.01f, dt ) );
		TurnLean = TurnLean.LerpTo( targetLean, 1.0f - MathF.Pow( 0.05f, dt ) );

		if ( backWheelsOnGround )
		{
			var forwardSpeed = MathF.Abs( localVelocity.x );
			var speedFactor = 1.0f - (forwardSpeed / 5000.0f).Clamp( 0.0f, 1.0f );
			var acceleration = speedFactor * (accelerateDirection < 0.0f ? vehicle_accelspeed * 0.5f : vehicle_accelspeed) * accelerateDirection * dt;
			var impulse = rotation * new Vector3( acceleration, 0, 0 );
			body.Velocity += impulse;
		}

		RaycastWheels( rotation, true, out frontWheelsOnGround, out backWheelsOnGround, dt );
		var onGround = frontWheelsOnGround || backWheelsOnGround;
		var fullyGrounded = (frontWheelsOnGround && backWheelsOnGround);
		Grounded = onGround;

		if ( fullyGrounded )
		{
			body.Velocity += PhysicsWorld.Gravity * dt;
		}

		body.GravityScale = fullyGrounded ? 0 : 1;

		bool canAirControl = false;

		var v = rotation * localVelocity.WithZ( 0 );
		var vDelta = MathF.Pow( (v.Length / 1000.0f).Clamp( 0, 1 ), 5.0f ).Clamp( 0, 1 );
		if ( vDelta < 0.01f ) vDelta = 0;

		if ( debug_vehicle )
		{
			DebugOverlay.Line( body.MassCenter, body.MassCenter + rotation.Forward.Normal * 100, Color.White, 0, false );
			DebugOverlay.Line( body.MassCenter, body.MassCenter + v.Normal * 100, Color.Green, 0, false );
		}

		var angle = (rotation.Forward.Normal * MathF.Sign( localVelocity.x )).Normal.Dot( v.Normal ).Clamp( 0.0f, 1.0f );
		angle = angle.LerpTo( 1.0f, 1.0f - vDelta );
		grip = grip.LerpTo( angle, 1.0f - MathF.Pow( 0.001f, dt ) );

		if ( debug_vehicle )
		{
			DebugOverlay.ScreenText( new Vector2( 200, 200 ), $"{grip}" );
		}

		var angularDamping = 0.0f;
		angularDamping = angularDamping.LerpTo( 2.0f - (localVelocity.x / 2000f - 250f).Clamp( 0f, 100f ) * MathF.Sign( MathF.Floor( localVelocity.x ) ), grip );

		body.LinearDamping = 0.0f;
		body.AngularDamping = fullyGrounded ? angularDamping : 0.7f;

		if ( onGround )
		{
			localVelocity = rotation.Inverse * body.Velocity;
			WheelSpeed = localVelocity.x;
			var turnAmount = frontWheelsOnGround ? (MathF.Sign( localVelocity.x ) * 15.0f * CalculateTurnFactor( TurnDirection, MathF.Abs( localVelocity.x ) ) * dt) : 0.0f;
			body.AngularVelocity += rotation * new Vector3( 0, 0, turnAmount );

			airRoll = 0;
			airTilt = 0;

			var forwardGrip = 0.1f;
			forwardGrip = forwardGrip.LerpTo( 0.9f, currentInput.breaking ) - (body.AngularVelocity.x - 100f).Clamp( 0, 23.75f );
			body.Velocity = VelocityDamping( Velocity, rotation, new Vector3( forwardGrip, grip - (body.AngularVelocity.x - 25f).Clamp( 0, 45.5f ), 0 ), dt );
		}
		else
		{
			var s = selfBody.Position + (rotation * selfBody.LocalMassCenter);
			var tr = Trace.Ray( s, s + rotation.Down * 50 )
				.Ignore( this )
				.Run();

			if ( debug_vehicle )
				DebugOverlay.Line( tr.StartPos, tr.EndPos, tr.Hit ? Color.Red : Color.Green );

			canAirControl = !tr.Hit;
		}

		if ( canAirControl && (airRoll != 0 || airTilt != 0) )
		{
			var offset = 50 * Scale;
			var s = selfBody.Position + (rotation * selfBody.LocalMassCenter) + (rotation.Right * airRoll * offset) + (rotation.Down * (10 * Scale));
			var tr = Trace.Ray( s, s + rotation.Up * (25 * Scale) )
				.Ignore( this )
				.Run();

			if ( debug_vehicle )
				DebugOverlay.Line( tr.StartPos, tr.EndPos );

			bool dampen = false;

			if ( currentInput.roll.Clamp( -1, 1 ) != 0 )
			{
				var force = tr.Hit ? 600.0f : 100.0f;
				var roll = tr.Hit ? currentInput.roll.Clamp( -1, 1 ) : airRoll;
				body.ApplyForceAt( selfBody.MassCenter + rotation.Left * (offset * roll), (rotation.Down * roll) * (roll * (body.Mass * force)) );

				if ( debug_vehicle )
					DebugOverlay.Sphere( selfBody.MassCenter + rotation.Left * (offset * roll), 8, Color.Red );

				dampen = true;
			}

			if ( !tr.Hit && currentInput.tilt.Clamp( -1, 1 ) != 0 )
			{
				var force = 200.0f;
				body.ApplyForceAt( selfBody.MassCenter + rotation.Forward * (offset * airTilt), (rotation.Down * airTilt) * (airTilt * (body.Mass * force)) );

				if ( debug_vehicle )
					DebugOverlay.Sphere( selfBody.MassCenter + rotation.Forward * (offset * airTilt), 8, Color.Green );

				dampen = true;
			}

			if ( dampen )
				body.AngularVelocity = VelocityDamping( body.AngularVelocity, rotation, 0.95f, dt );
		}

		localVelocity = rotation.Inverse * body.Velocity;
		MovementSpeed = localVelocity.x;
	}

	private static float CalculateTurnFactor( float direction, float speed )
	{
		var turnFactor = MathF.Min( speed / 500.0f, 1 );
		var yawSpeedFactor = 1.0f - (speed / 1000.0f).Clamp( 0, 0.6f );

		return direction * turnFactor * yawSpeedFactor;
	}

	private static Vector3 VelocityDamping( Vector3 velocity, Rotation rotation, Vector3 damping, float dt )
	{
		var localVelocity = rotation.Inverse * velocity;
		var dampingPow = new Vector3( MathF.Pow( 1.0f - damping.x, dt ), MathF.Pow( 1.0f - damping.y, dt ), MathF.Pow( 1.0f - damping.z, dt ) );
		return rotation * (localVelocity * dampingPow);
	}

	private void RaycastWheels( Rotation rotation, bool doPhysics, out bool frontWheels, out bool backWheels, float dt )
	{
		float forward = 42;
		float right = 34; //32
		float length = 25.0f;

		var frontLeftPos = rotation.Forward * forward + rotation.Right * right + rotation.Up * (length - 10);
		var frontRightPos = rotation.Forward * forward - rotation.Right * right + rotation.Up * (length - 10);
		var backLeftPos = -rotation.Forward * forward + rotation.Right * right + rotation.Up * (length - 10);
		var backRightPos = -rotation.Forward * forward - rotation.Right * right + rotation.Up * (length - 10);

		var tiltAmount = AccelerationTilt * 2.5f;
		var leanAmount = TurnLean * 2.5f;


		frontWheels =
			frontLeft.Raycast( length + tiltAmount - leanAmount, doPhysics, frontLeftPos * Scale, ref frontLeftDistance, dt ) |
			frontRight.Raycast( length + tiltAmount + leanAmount, doPhysics, frontRightPos * Scale, ref frontRightDistance, dt );

		backWheels =
			backLeft.Raycast( length - tiltAmount - leanAmount, doPhysics, backLeftPos * Scale, ref backLeftDistance, dt ) |
			backRight.Raycast( length - tiltAmount + leanAmount, doPhysics, backRightPos * Scale, ref backRightDistance, dt );
	}

	float wheelAngle = 0.0f;
	float wheelRevolute = 0.0f;

	[Event.Frame]
	public void OnFrame()
	{
		wheelAngle = wheelAngle.LerpTo( TurnDirection * 25, 1.0f - MathF.Pow( 0.001f, Time.Delta ) );
		wheelRevolute += (WheelSpeed / (14.0f * Scale)).RadianToDegree() * Time.Delta;

		var wheelSteeringR = Rotation.From( wheelAngle, 0, 0 );
		var wheelSteeringL = Rotation.From( wheelAngle - 180, 0, 0 );
		var wheelRotRight = Rotation.From( wheelRevolute, -90, 0 );  //Rotation.From( -wheelAngle, 180, -wheelRevolute );
		var wheelRotLeft = Rotation.From( -wheelRevolute, -90, 0 ); //Rotation.From( wheelAngle, 0, wheelRevolute );
		var wheelRotBackRight = Rotation.From( wheelRevolute, 180, 0 ); //Rotation.From( 0, 90, -wheelRevolute );
		var wheelRotBackLeft = Rotation.From( -wheelRevolute + 180, 0, 0 ); //Rotation.From( 0, -90, wheelRevolute );
		var steeringWheelRot = Rotation.From( 0, 90f + wheelAngle * 10f, 0 );

		RaycastWheels( Rotation, false, out _, out _, Time.Delta );

		float frontOffset = 15f - Math.Min( frontLeftDistance, frontRightDistance );
		float backOffset = 15f - Math.Min( backLeftDistance, backRightDistance );

		float frontAxleOffset = frontLeftDistance - frontRightDistance;
		float rearAxleOffset = backLeftDistance - backRightDistance;

		var axleRotFront = Rotation.From( 0, 0, frontAxleOffset );
		var axleRotRear = Rotation.From( 0, 0, rearAxleOffset );

		vehicle_steering_wheel.LocalRotation = steeringWheelRot;

		chassis_axle_front.SetBoneTransform( "Axle_front_Center", new Transform( Vector3.Up * frontOffset ), false );
		chassis_axle_rear.SetBoneTransform( "Axle_Rear_Center", new Transform( Vector3.Up * backOffset ), false );
		chassis_axle_front.LocalRotation = axleRotFront;
		chassis_axle_rear.LocalRotation = axleRotRear;

		wheel0_steering.LocalRotation = wheelSteeringR;
		wheel1_steering.LocalRotation = wheelSteeringL;

		wheel0.LocalRotation = wheelRotRight;
		wheel1.LocalRotation = wheelRotLeft;
		wheel2.LocalRotation = wheelRotBackRight;
		wheel3.LocalRotation = wheelRotBackLeft;
	}

	private void RemoveDriver( SandboxPlayer player )
	{
		driver = null;
		player.Vehicle = null;
		player.VehicleController = null;
		player.VehicleAnimator = null;
		player.VehicleCamera = null;
		player.Parent = null;
		player.PhysicsBody.Enabled = true;
		player.PhysicsBody.Position = player.Position;

		timeSinceDriverLeft = 0;

		ResetInput();
	}

	public bool OnUse( Entity user )
	{
		if ( user is SandboxPlayer player && player.Vehicle == null && timeSinceDriverLeft > 1.0f )
		{
			player.Vehicle = this;
			player.VehicleController = new Vehicle_controller();
			player.VehicleAnimator = new Vehicle_animator();
			player.VehicleCamera = new Vehicle_camera();
			player.Parent = this;
			player.LocalPosition = Vector3.Up * 10;
			player.LocalRotation = Rotation.Identity;
			player.LocalScale = 1;
			player.PhysicsBody.Enabled = false;

			driver = player;
		}

		return true;
	}

	public bool IsUsable( Entity user )
	{
		return driver == null;
	}

	public override void StartTouch( Entity other )
	{
		base.StartTouch( other );

		if ( !IsServer )
			return;

		var body = PhysicsBody;
		if ( !body.IsValid() )
			return;

		body = body.SelfOrParent;
		if ( !body.IsValid() )
			return;

		if ( other is SandboxPlayer player && player.Vehicle == null )
		{
			var speed = body.Velocity.Length;
			var forceOrigin = Position + Rotation.Down * Rand.Float( 20, 30 );
			var velocity = (player.Position - forceOrigin).Normal * speed;
			var angularVelocity = body.AngularVelocity;

			OnPhysicsCollision( new CollisionEventData
			{
				Entity = player,
				Pos = player.Position + Vector3.Up * 50,
				Velocity = velocity,
				PreVelocity = velocity * 20.0f, // I don't know why the ragdolls now need more force
				PostVelocity = velocity,
				PreAngularVelocity = angularVelocity,
				Speed = speed,
			} );
		}
	}

	protected override void OnPhysicsCollision( CollisionEventData eventData )
	{
		if ( !IsServer )
			return;

		if ( eventData.Entity is SandboxPlayer player && player.Vehicle != null )
		{
			return;
		}

		var propData = GetModelPropData();

		var minImpactSpeed = propData.MinImpactDamageSpeed;
		if ( minImpactSpeed <= 0.0f ) minImpactSpeed = 500;

		var impactDmg = propData.ImpactDamage;
		if ( impactDmg <= 0.0f ) impactDmg = 10;

		var speed = eventData.Speed;

		if ( speed > minImpactSpeed )
		{
			if ( eventData.Entity.IsValid() && eventData.Entity != this )
			{
				var damage = speed / minImpactSpeed * impactDmg * 1.2f;
				eventData.Entity.TakeDamage( DamageInfo.Generic( damage )
					.WithFlag( DamageFlags.PhysicsImpact )
					.WithFlag( DamageFlags.Vehicle )
					.WithAttacker( driver != null ? driver : this, driver != null ? this : null )
					.WithPosition( eventData.Pos )
					.WithForce( eventData.PreVelocity ) );

				if ( eventData.Entity.LifeState == LifeState.Dead && eventData.Entity is not SandboxPlayer )
				{
					PhysicsBody.Velocity = eventData.PreVelocity;
				}
			}
		}
	}
}