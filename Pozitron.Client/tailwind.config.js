/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{vue,js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        brand: {
          light: '#a855f7',
          DEFAULT: '#8b5cf6',
          dark: '#7c3aed',
        }
      }
    },
  },
  plugins: [],
}