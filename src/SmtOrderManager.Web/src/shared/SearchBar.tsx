interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  onSearch: () => void;
}

export function SearchBar({ value, onChange, onSearch }: SearchBarProps) {
  return (
    <form
      className="search"
      onSubmit={(event) => {
        event.preventDefault();
        onSearch();
      }}
    >
      <input
        type="search"
        placeholder="Search name or description"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
      <button type="submit">Search</button>
    </form>
  );
}
