import { MagnifyingGlassIcon } from "@heroicons/react/24/outline";
import React from "react";

// Define the props for the SearchInput component
interface SearchInputProps {
  placeholder?: string;
  className?: string;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void;
  value?: string;
  size?: "xs" | "sm" | "md" | "lg";
  variant?: "bordered" | "ghost" | "primary" | "secondary";
}

const SearchInput: React.FC<SearchInputProps> = ({
  placeholder = "Search...",
  className = "",
  onChange,
  value,
  size = "md",
  variant = "bordered",
}) => {
  // Map size to DaisyUI classes
  const sizeClasses = {
    xs: "input-xs",
    sm: "input-sm",
    md: "input-md",
    lg: "input-lg",
  };

  // Map variant to DaisyUI classes
  const variantClasses = {
    bordered: "input-bordered",
    ghost: "input-ghost",
    primary: "input-primary",
    secondary: "input-secondary",
  };

  // Icon size based on input size
  const iconSizes = {
    xs: "w-3 h-3",
    sm: "w-4 h-4",
    md: "w-5 h-5",
    lg: "w-6 h-6",
  };

  // Padding adjustments based on size
  const paddingClasses = {
    xs: "pl-7 pr-2",
    sm: "pl-8 pr-3",
    md: "pl-10 pr-4",
    lg: "pl-12 pr-4",
  };

  return (
    <div className={`relative ${className}`}>
      {/* Search icon */}
      <MagnifyingGlassIcon
        className={`absolute left-3 top-1/2 -translate-y-1/2 ${iconSizes[size]} text-base-content/50 pointer-events-none`}
        aria-hidden="true"
      />

      {/* Input field */}
      <input
        type="text"
        value={value}
        placeholder={placeholder}
        className={`
          input 
          ${sizeClasses[size]} 
          ${variantClasses[variant]}
          ${paddingClasses[size]}
          w-full
          bg-base-100
          text-base-content
          placeholder:text-base-content/40
          focus:outline-none 
          focus:border-dynamic-blue
          transition-colors
        `}
        onChange={onChange}
        aria-label={placeholder}
      />
    </div>
  );
};

export default SearchInput;
