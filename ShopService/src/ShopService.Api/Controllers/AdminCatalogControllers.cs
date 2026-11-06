using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopService.Api.Extensions;
using ShopService.Application.Common;
using ShopService.Application.Contracts;
using ShopService.Application.Services;
using ShopService.Domain;

namespace ShopService.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/categories")]
[Produces("application/json")]
public sealed class AdminCategoriesController(CategoryService categoryService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryRequest request, CancellationToken ct) =>
        this.ToCreated(await categoryService.CreateAsync(request, ct),
            nameof(CategoriesController.Get), c => new { controller = "Categories", id = c.Id });

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Update(CategoryId id, CategoryRequest request, CancellationToken ct) =>
        this.ToActionResult(await categoryService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(CategoryId id, CancellationToken ct) =>
        this.ToNoContent(await categoryService.DeleteAsync(id, ct));
}

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/products")]
[Produces("application/json")]
public sealed class AdminProductsController(ProductService productService) : ControllerBase
{
    /// <summary>Jak publiczna lista, ale razem z produktami nieaktywnymi.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductListItem>>(StatusCodes.Status200OK)]
    public async Task<PagedResponse<ProductListItem>> Search([FromQuery] ProductSearchRequest request, CancellationToken ct) =>
        await productService.SearchAsync(request, includeInactive: true, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Get(ProductId id, CancellationToken ct) =>
        this.ToActionResult(await productService.GetAsync(id, includeInactive: true, ct));

    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> Create(ProductRequest request, CancellationToken ct) =>
        this.ToCreated(await productService.CreateAsync(request, ct), nameof(Get), p => new { id = p.Id });

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Update(ProductId id, ProductRequest request, CancellationToken ct) =>
        this.ToActionResult(await productService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(ProductId id, CancellationToken ct) =>
        this.ToNoContent(await productService.DeleteAsync(id, ct));
}
