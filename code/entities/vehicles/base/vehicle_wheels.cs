﻿using Sandbox;
using System;

struct Vehicle_wheels
{
	private readonly Vehicle_base parent;

	private float _previousLength;
	private float _currentLength;

	public Vehicle_wheels( Vehicle_base parent )
	{
		this.parent = parent;
		_previousLength = 0;
		_currentLength = 0;
	}

	public bool Raycast( float length, bool doPhysics, Vector3 offset, ref float wheel, float dt )
	{
		var position = parent.Position;
		position.z = parent.Position.z + 2f;
		var rotation = parent.Rotation;

		var wheelAttachPos = position + offset;
		var wheelExtend = wheelAttachPos - rotation.Up * (length * parent.Scale);

		var tr = Trace.Ray( wheelAttachPos, wheelExtend )
			.Ignore( parent )
			.Ignore( parent.driver )
			.Run();

		wheel = length * tr.Fraction;
		var wheelRadius = (14 * parent.Scale);

		if ( !doPhysics && Vehicle_entity.debug_vehicle )
		{
			var wheelPosition = tr.Hit ? tr.EndPos : wheelExtend;
			wheelPosition += rotation.Up * wheelRadius;

			if ( tr.Hit )
			{
				DebugOverlay.Circle( wheelPosition, rotation * Rotation.FromYaw( 90 ), wheelRadius, Color.Red.WithAlpha( 0.5f ), false );
				DebugOverlay.Line( tr.StartPos, tr.EndPos, Color.Red, 0, false );
			}
			else
			{
				DebugOverlay.Circle( wheelPosition, rotation * Rotation.FromYaw( 90 ), wheelRadius, Color.Green.WithAlpha( 0.5f ), false );
				DebugOverlay.Line( wheelAttachPos, wheelExtend, Color.Green, 0, false );
			}
		}

		if ( !tr.Hit || !doPhysics )
		{
			return tr.Hit;
		}

		var body = parent.PhysicsBody.SelfOrParent;

		_previousLength = _currentLength;
		_currentLength = (length * parent.Scale) - tr.Distance;

		var springVelocity = (_currentLength - _previousLength) / dt;
		var springForce = body.Mass * 25.0f * _currentLength; //50.0f
		var damperForce = body.Mass * (1.5f + (1.0f - tr.Fraction) * 2.0f) * springVelocity; //3.0f
		var velocity = body.GetVelocityAtPoint( wheelAttachPos );
		var speed = velocity.Length;
		var speedDot = MathF.Abs( speed ) > 0.0f ? MathF.Abs( MathF.Min( Vector3.Dot( velocity, rotation.Up.Normal ) / speed, 0.0f ) ) : 0.0f;
		var speedAlongNormal = speedDot * speed;
		var correctionMultiplier = (1.0f - tr.Fraction) * (speedAlongNormal / 1000.0f);
		var correctionForce = correctionMultiplier * 35.0f * speedAlongNormal / dt; //50.0f

		body.ApplyImpulseAt( wheelAttachPos, tr.Normal * (springForce + damperForce + correctionForce) * dt );

		return true;
	}
}
