using System;
using JetBrains.Annotations;

namespace GameFinder
{
    /// <summary>
    /// Abstract class of a Store Game
    /// </summary>
    [PublicAPI]
    public abstract class AStoreGame : IComparable<AStoreGame>, IEquatable<AStoreGame>, ICloneable
    {
        /// <summary>
        /// Name of the Game
        /// </summary>
        public virtual string Name { get; set; } = string.Empty;

        /// <summary>
        /// Path to the Game
        /// </summary>
        public virtual string Path { get; set; } = string.Empty;

        /// <summary>
        /// Store Type
        /// </summary>
        public abstract StoreType StoreType { get; }

        #region Overrides

        /// <inheritdoc cref="IComparable{T}.CompareTo"/>
        public virtual int CompareTo(AStoreGame? other)
        {
            return string.Compare(Name, other?.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public virtual bool Equals(AStoreGame? other)
        {
            return string.Equals(Name, other?.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc cref="object.Equals(object?)"/>
        public override bool Equals(object? obj)
        {
            return obj switch
            {
                null => false,
                AStoreGame aStoreGame => Equals(aStoreGame),
                _ => false
            };
        }

        /// <inheritdoc cref="object.GetHashCode"/>
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }

        /// <inheritdoc cref="object.ToString"/>
        public override string ToString()
        {
            return $"{Name}";
        }

        /// <inheritdoc />
        public abstract object Clone();

        #endregion
    }
}
