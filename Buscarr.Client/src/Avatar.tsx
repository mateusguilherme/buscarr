export function Avatar({ name, large = false }: { name: string; large?: boolean }) {
  return (
    <span className={large ? 'avatar avatar-large' : 'avatar'} aria-hidden="true">
      {[...name][0]?.toUpperCase()}
    </span>
  );
}
