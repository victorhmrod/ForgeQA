using ForgeQA.Application.Common;
using ForgeQA.Application.Projects;
using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Application;

public class ProjectServiceTests
{
    private static (ProjectService Service, FakeOrganizationRepository Orgs, FakeProjectRepository Projects, Guid OrgId, Guid UserId) CreateSut()
    {
        var orgs = new FakeOrganizationRepository();
        var projects = new FakeProjectRepository();
        var userId = Guid.NewGuid();

        var organization = new Organization("Victor's Workspace", "victors-workspace");
        organization.AddMember(userId, OrganizationRole.Owner);
        orgs.Add(organization);

        var service = new ProjectService(projects, orgs, new FakeUnitOfWork());
        return (service, orgs, projects, organization.Id, userId);
    }

    [Fact]
    public async Task CreateAsync_Generates_Slug_From_Name()
    {
        var (service, _, _, orgId, userId) = CreateSut();

        var result = await service.CreateAsync(orgId, userId, new CreateProjectRequest("FRONTLINE", "Competitive FPS"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Slug.Should().Be("frontline");
    }

    [Fact]
    public async Task CreateAsync_Appends_Suffix_When_Slug_Already_Exists_In_Organization()
    {
        var (service, _, _, orgId, userId) = CreateSut();
        await service.CreateAsync(orgId, userId, new CreateProjectRequest("FRONTLINE", null), CancellationToken.None);

        var result = await service.CreateAsync(orgId, userId, new CreateProjectRequest("FRONTLINE", null), CancellationToken.None);

        result.Value!.Slug.Should().Be("frontline-2");
    }

    [Fact]
    public async Task CreateAsync_Fails_When_User_Is_Not_A_Member()
    {
        var (service, _, _, orgId, _) = CreateSut();
        var outsider = Guid.NewGuid();

        var result = await service.CreateAsync(orgId, outsider, new CreateProjectRequest("FRONTLINE", null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetByIdAsync_Fails_When_User_Is_Not_A_Member_Of_The_Projects_Organization()
    {
        var (service, _, _, orgId, userId) = CreateSut();
        var created = await service.CreateAsync(orgId, userId, new CreateProjectRequest("FRONTLINE", null), CancellationToken.None);
        var outsider = Guid.NewGuid();

        var result = await service.GetByIdAsync(created.Value!.Id, outsider, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }
}
