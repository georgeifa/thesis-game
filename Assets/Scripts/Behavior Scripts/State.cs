using System;
using Unity.Behavior;

[BlackboardEnum]
public enum State
{
	Spawn,
    Idle,
	Patrol,
	Alert,
	Chase,
	Attack,
	UsingSkill
}
