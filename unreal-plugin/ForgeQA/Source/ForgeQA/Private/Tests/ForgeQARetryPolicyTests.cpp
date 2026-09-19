#include "CoreMinimal.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "ForgeQARetryPolicy.h"

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQARetryPolicyClassificationTest, "ForgeQA.RetryPolicy.ClassifiesFailuresCorrectly",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQARetryPolicyClassificationTest::RunTest(const FString& Parameters)
{
    // Transient: never reached the server, rate limiting, and server errors should be retried.
    FForgeQAApiError NetworkError; NetworkError.StatusCode = 0;
    FForgeQAApiError RateLimited; RateLimited.StatusCode = 429;
    FForgeQAApiError ServerError; ServerError.StatusCode = 503;

    // Permanent: bad credentials/request, and (Performance-specific) not-found/ended-session
    // responses should never be retried.
    FForgeQAApiError Unauthorized; Unauthorized.StatusCode = 401;
    FForgeQAApiError Forbidden; Forbidden.StatusCode = 403;
    FForgeQAApiError BadRequest; BadRequest.StatusCode = 400;
    FForgeQAApiError NotFound; NotFound.StatusCode = 404;
    FForgeQAApiError SessionEnded; SessionEnded.StatusCode = 409;

    TestTrue(TEXT("A network failure is transient"), FForgeQARetryPolicy::IsTransientFailure(NetworkError));
    TestTrue(TEXT("A 429 is transient"), FForgeQARetryPolicy::IsTransientFailure(RateLimited));
    TestTrue(TEXT("A 5xx is transient"), FForgeQARetryPolicy::IsTransientFailure(ServerError));
    TestFalse(TEXT("A 401 is permanent"), FForgeQARetryPolicy::IsTransientFailure(Unauthorized));
    TestFalse(TEXT("A 403 is permanent"), FForgeQARetryPolicy::IsTransientFailure(Forbidden));
    TestFalse(TEXT("A 400 is permanent"), FForgeQARetryPolicy::IsTransientFailure(BadRequest));
    TestFalse(TEXT("A 404 is permanent"), FForgeQARetryPolicy::IsTransientFailure(NotFound));
    TestFalse(TEXT("A 409 (ended session) is permanent"), FForgeQARetryPolicy::IsTransientFailure(SessionEnded));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQARetryPolicyBackoffTest, "ForgeQA.RetryPolicy.DelayGrowsAndIsCapped",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQARetryPolicyBackoffTest::RunTest(const FString& Parameters)
{
    // Each attempt's delay (before jitter) should roughly double, and never exceed the cap even at
    // a high attempt count.
    TestTrue(TEXT("Attempt 1 delay is at least the 1s base"), FForgeQARetryPolicy::DelayForAttempt(1) >= 1.0);
    TestTrue(TEXT("Attempt 4 delay is larger than attempt 1"), FForgeQARetryPolicy::DelayForAttempt(4) > FForgeQARetryPolicy::DelayForAttempt(1));
    TestTrue(TEXT("A very high attempt count is capped near MaxRetryDelaySeconds"),
        FForgeQARetryPolicy::DelayForAttempt(20) <= FForgeQARetryPolicy::MaxRetryDelaySeconds * 1.2);
    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
