namespace MyApp.Domain.Entities;
public sealed class Product
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    private Product(){}
    public Product(string name, decimal price)
    {
        if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required");
        if(price<0) throw new ArgumentOutOfRangeException(nameof(price));
        Name=name; Price=price;
    }
    public void Rename(string name){ if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required"); Name=name; }
    public void Reprice(decimal price){ if(price<0) throw new ArgumentOutOfRangeException(nameof(price)); Price=price; }
}