---
name: impeccable-ptmanager
description: |
  Design excellence for PT Manager frontend. Use when building UI components, pages, or modifying styles in React + Tailwind CSS + Chakra UI. Triggers on: creating new pages, building components, adjusting layouts, styling forms, designing dashboards, reviewing UI/UX, improving accessibility. Delivers cohesive, accessible, performance-optimized design that respects PT Manager's brand and user patterns.
---

# Impeccable: Design Skill for PT Manager Frontend

You are designing user interfaces for PT Manager — a SaaS dashboard for personal trainers. Every component, page, and interaction should feel intentional, consistent, and accessible.

## Design Principles for PT Manager

### 1. Clarity Over Decoration

PT Manager serves busy personal trainers. They need information fast. No gradients-for-gradients-sake, no animations without purpose.

- **Information hierarchy:** Largest text for what matters most. Related data grouped visually.
- **White space:** Breathing room between sections. Dense data tables still need padding.
- **Color:** Purposeful. Blue for primary actions, red for destructive, green for success. No random accent colors.
- **Typography:** Clear sans-serif (Rubik, Inter, or system fonts). 16px base for readability. Don't go smaller than 12px for UI text.

### 2. Accessibility is Not Optional

Every component must:
- Contrast: WCAG AA minimum (4.5:1 for text, 3:1 for graphics)
- Keyboard navigation: Tab through interactive elements, Enter/Space to activate
- Screen readers: `aria-label`, `aria-describedby`, semantic HTML (`<button>`, `<nav>`, `<main>`)
- Color blind safe: Don't rely on color alone (e.g., use icon + color for status)
- Touch targets: Buttons/links 44px minimum (mobile), 32px minimum (desktop)

**Use Chakra UI's built-in a11y props heavily:**
```jsx
<Button aria-label="Delete client" colorScheme="red">
  Delete
</Button>
```

### 3. Consistency: Build Once, Use Everywhere

PT Manager uses **Tailwind CSS 4** + **Chakra UI** + **shadcn/ui** for base components. Use existing components first.

- **Pages (.jsx):** Feature pages, layout, data flow
- **UI Components (.tsx):** Reusable button, card, modal, form field from shadcn/ui or Chakra
- **Tailwind:** Only for custom layout/spacing when no component exists
- **Never duplicate:** If a button pattern exists, use it. Don't create a new one.

### 4. Responsive Design: Mobile-First

PT Manager users are on phones during gym sessions. Every page must work at 375px width.

```jsx
// Good: Stack on mobile, row on desktop
<Flex direction={{ base: 'column', md: 'row' }} gap={4}>
  <Box flex={1}>Client List</Box>
  <Box flex={1}>Details</Box>
</Flex>
```

**Breakpoints (Chakra defaults):**
- `base`: 0px (mobile)
- `sm`: 640px
- `md`: 768px (tablet)
- `lg`: 1024px (desktop)
- `xl`: 1280px (wide)

### 5. Dark/Light Mode Support

PT Manager supports dark mode. Every color must work in both.

- Use Chakra's `useColorMode()` and `_dark` pseudo-selectors
- Test in both modes before shipping
- Don't hardcode colors — use theme variables

```jsx
<Box bg={{ base: 'white', _dark: 'gray.900' }} />
```

Or use Chakra's semantic colors:
```jsx
<Box bg="bg.surface" color="fg.default" />
```

## Component Patterns

### Form Fields: Consistent Input

```jsx
// Good: Use Chakra form control for labels, errors, helper text
import { FormControl, FormLabel, FormErrorMessage, Input } from '@chakra-ui/react';

export function ClientNameField({ value, onChange, error }) {
  return (
    <FormControl isInvalid={!!error}>
      <FormLabel>Full Name</FormLabel>
      <Input 
        value={value} 
        onChange={(e) => onChange(e.target.value)}
        placeholder="John Doe"
      />
      {error && <FormErrorMessage>{error}</FormErrorMessage>}
    </FormControl>
  );
}
```

### Cards: Data Containers

```jsx
import { Box, HStack, VStack, Text, Button } from '@chakra-ui/react';

export function ClientCard({ client, onEdit }) {
  return (
    <Box 
      p={6} 
      borderWidth={1} 
      borderRadius="md" 
      shadow="sm"
      _hover={{ shadow: 'md' }}
      transition="shadow 0.2s"
    >
      <VStack align="start" gap={3}>
        <Text fontWeight="bold" fontSize="lg">{client.name}</Text>
        <Text color="gray.500" fontSize="sm">{client.email}</Text>
        <HStack>
          <Button size="sm" onClick={onEdit}>Edit</Button>
        </HStack>
      </VStack>
    </Box>
  );
}
```

**Avoid:**
- Hardcoded padding/margins — use Chakra spacing tokens (2, 4, 6, 8)
- Custom shadows — use Chakra's shadow scale (`sm`, `md`, `lg`)
- Hover states without transition — 0.2s-0.3s transition is standard

### Tables: Readable Data

```jsx
import { Table, Thead, Tbody, Tr, Th, Td, Box } from '@chakra-ui/react';

export function SessionsTable({ sessions }) {
  return (
    <Box overflowX="auto">
      <Table size="sm">
        <Thead bg="gray.100" _dark={{ bg: 'gray.800' }}>
          <Tr>
            <Th>Date</Th>
            <Th>Duration</Th>
            <Th>Status</Th>
          </Tr>
        </Thead>
        <Tbody>
          {sessions.map((session) => (
            <Tr key={session.id} _hover={{ bg: 'gray.50' }} _dark={{ _hover: { bg: 'gray.800' } }}>
              <Td>{new Date(session.date).toLocaleDateString()}</Td>
              <Td>{session.durationMinutes} min</Td>
              <Td><Badge colorScheme={session.status === 'completed' ? 'green' : 'gray'}>{session.status}</Badge></Td>
            </Tr>
          ))}
        </Tbody>
      </Table>
    </Box>
  );
}
```

### Modals: Intentional Dialogs

```jsx
import { 
  Modal, 
  ModalOverlay, 
  ModalContent, 
  ModalHeader, 
  ModalFooter, 
  ModalBody, 
  ModalCloseButton,
  Button 
} from '@chakra-ui/react';

export function DeleteClientModal({ isOpen, onClose, onConfirm }) {
  return (
    <Modal isOpen={isOpen} onClose={onClose} isCentered>
      <ModalOverlay />
      <ModalContent>
        <ModalHeader>Delete Client</ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          Are you sure? This cannot be undone.
        </ModalBody>
        <ModalFooter gap={3}>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button colorScheme="red" onClick={onConfirm}>Delete</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}
```

## Performance: Keep Pages Snappy

- **Image optimization:** Use `<Image />` from next/image or Chakra, with lazy loading
- **Code splitting:** Use React.lazy() for non-critical pages
- **Avoid re-renders:** Use `useMemo` and `useCallback` sparingly (only when profiler shows real impact)
- **Bundle size:** Tree-shake unused Chakra components in barrel exports

## PT Manager-Specific Patterns

### Navigation: Consistent Structure

```jsx
<HStack spacing={1}>
  <NavLink href="/dashboard" active={isActive('/dashboard')}>Dashboard</NavLink>
  <NavLink href="/clients" active={isActive('/clients')}>Clients</NavLink>
  <NavLink href="/sessions" active={isActive('/sessions')}>Sessions</NavLink>
  <NavLink href="/billing" active={isActive('/billing')}>Billing</NavLink>
</HStack>
```

### Status Badges: Semantic Colors

```jsx
const statusColors = {
  pending: 'yellow',
  completed: 'green',
  cancelled: 'red',
  scheduled: 'blue',
};

<Badge colorScheme={statusColors[status]}>{status}</Badge>
```

### Forms: Always Validate Visually

```jsx
<FormControl isInvalid={touched && !!errors.email}>
  <FormLabel>Email</FormLabel>
  <Input 
    type="email"
    value={email}
    onChange={handleChange}
    onBlur={handleBlur}
  />
  {touched && errors.email && <FormErrorMessage>{errors.email}</FormErrorMessage>}
</FormControl>
```

## Color Palette (PT Manager)

Use Chakra's default palette. Recommended for PT Manager:

- **Primary actions:** `blue.500` (button, links)
- **Success:** `green.500` (session completed, payment confirmed)
- **Warning:** `yellow.500` (upcoming deadline, low stock)
- **Destructive:** `red.500` (delete, cancel, error)
- **Neutral:** `gray.*` (backgrounds, borders, secondary text)

**Don't hardcode colors — use semantic names:**
```jsx
// Good
<Button colorScheme="blue">Create Session</Button>

// Avoid
<Button bg="#1e90ff">Create Session</Button>
```

## Checklist: Before Shipping a Component

- ✓ Semantic HTML (`<button>`, `<label>`, `<nav>`, not all `<div>`)
- ✓ ARIA labels on icons and screen-reader-only text
- ✓ Keyboard accessible (Tab, Enter, Space, Escape where needed)
- ✓ Works at 375px width (mobile)
- ✓ Responsive: test on mobile, tablet, desktop
- ✓ Dark mode: toggle and verify
- ✓ Touch target 44px+ (mobile), 32px+ (desktop)
- ✓ Contrast 4.5:1 (text/background)
- ✓ Color + icon/text for status (not color alone)
- ✓ No hardcoded colors — use theme/Chakra
- ✓ Reuses existing components (no duplication)
- ✓ No layout shift on load
- ✓ Error messages are clear and actionable

**When all checks pass, it's impeccable.**
