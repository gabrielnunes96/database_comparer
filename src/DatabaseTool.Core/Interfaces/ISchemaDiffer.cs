using DatabaseTool.Core.Diff;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Interfaces;

public interface ISchemaDiffer
{
    DiffReport Compare(SchemaModel source, SchemaModel target);
}
