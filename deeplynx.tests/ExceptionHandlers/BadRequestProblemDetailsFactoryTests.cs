using deeplynx.helpers.ExceptionHandlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace deeplynx.tests.ExceptionHandlers;

public class BadRequestProblemDetailsFactoryTests
{
    [Fact]
    public void Create_ReturnsBadRequestProblemDetails()
    {
        // Act
        var problem = BadRequestProblemDetailsFactory.Create("bad request");

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal(BadRequestProblemDetailsFactory.ProblemType, problem.Type);
        Assert.Equal("bad request", problem.Detail);
    }

    // Valid JSON, wrong type
    [Fact]
    public void CreateForModelState_CleansJsonPathKeys()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.labelId", "Label ID must be a valid number.");

        // Act
        var problem = BadRequestProblemDetailsFactory.CreateForModelState(
            modelState,
            new List<ParameterDescriptor>());

        // Assert
        Assert.True(problem.Errors.ContainsKey("labelId"));
        Assert.False(problem.Errors.ContainsKey("$.labelId"));
        Assert.Equal("Label ID must be a valid number.", problem.Errors["labelId"].Single());
    }

    // Malformed JSON at field
    [Fact]
    public void CreateForModelState_NormalizesSystemTextJsonMessage()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(
            "$.labelId",
            "'}' is an invalid start of a value. Path: $.labelId | LineNumber: 3 | BytePositionInLine: 0.");

        // Act
        var problem = BadRequestProblemDetailsFactory.CreateForModelState(
            modelState,
            new List<ParameterDescriptor>());

        // Assert
        Assert.Equal("'}' is an invalid start of a value.", problem.Errors["labelId"].Single());
    }

    // Empty body
    [Fact]
    public void CreateForModelState_RemovesBodyParameterLevelErrors()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("dto", "The dto field is required.");
        modelState.AddModelError("$.labelId", "Label ID must be a valid number.");

        var parameters = new List<ParameterDescriptor>
        {
            new()
            {
                Name = "dto",
                BindingInfo = new BindingInfo
                {
                    BindingSource = BindingSource.Body
                }
            }
        };

        // Act
        var problem = BadRequestProblemDetailsFactory.CreateForModelState(modelState, parameters);

        // Assert
        Assert.False(problem.Errors.ContainsKey("dto"));
        Assert.True(problem.Errors.ContainsKey("labelId"));
        Assert.Equal("Label ID must be a valid number.", problem.Errors["labelId"].Single());
    }

    // Empty object
    [Fact]
    public void CreateForModelState_PreservesRequiredFieldErrors_FromEmptyObject()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "The name field is required.");

        // Act
        var problem = BadRequestProblemDetailsFactory.CreateForModelState(
            modelState,
            new List<ParameterDescriptor>());

        // Assert
        Assert.True(problem.Errors.ContainsKey("name"));
        Assert.Equal("The name field is required.", problem.Errors["name"].Single());
    }

    // Missing required query parameter
    [Fact]
    public void CreateForModelState_PreservesMissingRequiredQueryParameterErrors()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("organizationId", "The organizationId field is required.");

        var parameters = new List<ParameterDescriptor>
        {
            new()
            {
                Name = "organizationId",
                BindingInfo = new BindingInfo
                {
                    BindingSource = BindingSource.Query
                }
            }
        };

        // Act
        var problem = BadRequestProblemDetailsFactory.CreateForModelState(modelState, parameters);

        // Assert
        Assert.True(problem.Errors.ContainsKey("organizationId"));
        Assert.Equal("The organizationId field is required.", problem.Errors["organizationId"].Single());
    }
    
    // Invalid query parameter type
    [Fact]
    public void CreateForModelState_PreservesQueryParameterErrors()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("organizationId", "The value 'abc' is not valid.");

        var parameters = new List<ParameterDescriptor>
        {
            new()
            {
                Name = "organizationId",
                BindingInfo = new BindingInfo
                {
                    BindingSource = BindingSource.Query
                }
            }
        };

        // Act
        var problem = BadRequestProblemDetailsFactory.CreateForModelState(modelState, parameters);

        // Assert
        Assert.True(problem.Errors.ContainsKey("organizationId"));
        Assert.Equal("The value 'abc' is not valid.", problem.Errors["organizationId"].Single());
    }

    // Verify array body errors preserve item context
    [Fact]
    public void CreateForModelState_PreservesArrayIndexContext()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(
            "$[0].name",
            "The JSON value could not be converted to System.String.");

        // Act
        var problem = BadRequestProblemDetailsFactory.CreateForModelState(
            modelState,
            new List<ParameterDescriptor>());

        // Assert
        Assert.True(problem.Errors.ContainsKey("[0].name"));
        Assert.False(problem.Errors.ContainsKey("$[0].name"));
        Assert.Equal(
            "The JSON value could not be converted to System.String.",
            problem.Errors["[0].name"].Single());
    }

    // Multiple invalid body fields
    [Fact]
    public void CreateForModelState_PreservesMultipleInvalidFields()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.labelId", "Label ID must be a valid number.");
        modelState.AddModelError("$.name", "The name field is required.");

        // Act
        var problem = BadRequestProblemDetailsFactory.CreateForModelState(
            modelState,
            new List<ParameterDescriptor>());

        // Assert
        Assert.True(problem.Errors.ContainsKey("labelId"));
        Assert.True(problem.Errors.ContainsKey("name"));
        Assert.Equal("Label ID must be a valid number.", problem.Errors["labelId"].Single());
        Assert.Equal("The name field is required.", problem.Errors["name"].Single());
    }

    // Body parameter name not dto
    [Fact]
    public void CreateForModelState_RemovesBodyParameterLevelErrors_WhenParameterNameIsRequest()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("request", "The request field is required.");
        modelState.AddModelError("$.fileName", "File name is required.");

        var parameters = new List<ParameterDescriptor>
        {
            new()
            {
                Name = "request",
                BindingInfo = new BindingInfo
                {
                    BindingSource = BindingSource.Body
                }
            }
        };

        // Act
        var problem = BadRequestProblemDetailsFactory.CreateForModelState(modelState, parameters);

        // Assert
        Assert.False(problem.Errors.ContainsKey("request"));
        Assert.True(problem.Errors.ContainsKey("fileName"));
        Assert.Equal("File name is required.", problem.Errors["fileName"].Single());
    }
}