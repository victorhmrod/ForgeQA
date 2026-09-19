#include "ForgeQARetryPolicy.h"

bool FForgeQARetryPolicy::IsTransientFailure(const FForgeQAApiError& Error)
{
    return Error.StatusCode == 0 || Error.StatusCode == 429 || Error.StatusCode >= 500;
}

double FForgeQARetryPolicy::DelayForAttempt(int32 Attempt)
{
    const double Base = FMath::Min(MaxRetryDelaySeconds, FMath::Pow(2.0, static_cast<double>(Attempt)));
    const double Jitter = FMath::FRandRange(0.0, Base * 0.2);
    return Base + Jitter;
}
