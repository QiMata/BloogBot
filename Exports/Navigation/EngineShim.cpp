#include "PhysicsEngine.h"

PhysicsOutput EngineStepShim(PhysicsEngine* self, const PhysicsInput& input, float dt)
{
    if (!self)
    {
        return PhysicsOutput{};
    }
    return self->Step(input, dt);
}
