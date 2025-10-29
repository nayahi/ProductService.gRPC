using FluentValidation;
using global::ECommerceGRPC.ProductService;

namespace ProductService.gRPC.Validators
{
    /// <summary>
    /// Validador para solicitudes de creación de productos
    /// </summary>
    public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
    {
        public CreateProductRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("El nombre del producto es requerido")
                .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres")
                .MinimumLength(3).WithMessage("El nombre debe tener al menos 3 caracteres");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("La descripción no puede exceder 1000 caracteres");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("El precio debe ser mayor a cero")
                .LessThan(1000000).WithMessage("El precio no puede exceder 1,000,000");

            RuleFor(x => x.Stock)
                .GreaterThanOrEqualTo(0).WithMessage("El stock no puede ser negativo")
                .LessThan(100000).WithMessage("El stock no puede exceder 100,000 unidades");

            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("La categoría es requerida")
                .MaximumLength(100).WithMessage("La categoría no puede exceder 100 caracteres");
        }
    }

    /// <summary>
    /// Validador para solicitudes de actualización de productos
    /// </summary>
    public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
    {
        public UpdateProductRequestValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("El ID del producto debe ser mayor a cero");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("El nombre del producto es requerido")
                .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres")
                .MinimumLength(3).WithMessage("El nombre debe tener al menos 3 caracteres");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("La descripción no puede exceder 1000 caracteres");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("El precio debe ser mayor a cero")
                .LessThan(1000000).WithMessage("El precio no puede exceder 1,000,000");

            RuleFor(x => x.Stock)
                .GreaterThanOrEqualTo(0).WithMessage("El stock no puede ser negativo")
                .LessThan(100000).WithMessage("El stock no puede exceder 100,000 unidades");

            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("La categoría es requerida")
                .MaximumLength(100).WithMessage("La categoría no puede exceder 100 caracteres");
        }
    }

    /// <summary>
    /// Validador para solicitudes de obtención de producto por ID
    /// </summary>
    public class GetProductRequestValidator : AbstractValidator<GetProductRequest>
    {
        public GetProductRequestValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("El ID del producto debe ser mayor a cero");
        }
    }

    /// <summary>
    /// Validador para solicitudes de listado de productos
    /// </summary>
    public class GetProductsRequestValidator : AbstractValidator<GetProductsRequest>
    {
        public GetProductsRequestValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThan(0).WithMessage("El número de página debe ser mayor a cero");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("El tamaño de página debe ser mayor a cero")
                .LessThanOrEqualTo(100).WithMessage("El tamaño de página no puede exceder 100 elementos");

            RuleFor(x => x.MinPrice)
                .GreaterThanOrEqualTo(0).WithMessage("El precio mínimo no puede ser negativo")
                .When(x => x.MinPrice > 0);

            RuleFor(x => x.MaxPrice)
                .GreaterThan(0).WithMessage("El precio máximo debe ser mayor a cero")
                .GreaterThanOrEqualTo(x => x.MinPrice).WithMessage("El precio máximo debe ser mayor o igual al precio mínimo")
                .When(x => x.MaxPrice > 0);
        }
    }

    /// <summary>
    /// Validador para solicitudes de búsqueda de productos
    /// </summary>
    public class SearchProductsRequestValidator : AbstractValidator<SearchProductsRequest>
    {
        public SearchProductsRequestValidator()
        {
            RuleFor(x => x.SearchTerm)
                .NotEmpty().WithMessage("El término de búsqueda es requerido")
                .MinimumLength(2).WithMessage("El término de búsqueda debe tener al menos 2 caracteres")
                .MaximumLength(100).WithMessage("El término de búsqueda no puede exceder 100 caracteres");

            RuleFor(x => x.PageNumber)
                .GreaterThan(0).WithMessage("El número de página debe ser mayor a cero");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("El tamaño de página debe ser mayor a cero")
                .LessThanOrEqualTo(100).WithMessage("El tamaño de página no puede exceder 100 elementos");
        }
    }

    /// <summary>
    /// Validador para solicitudes de eliminación de productos
    /// </summary>
    public class DeleteProductRequestValidator : AbstractValidator<DeleteProductRequest>
    {
        public DeleteProductRequestValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("El ID del producto debe ser mayor a cero");
        }
    }
}

