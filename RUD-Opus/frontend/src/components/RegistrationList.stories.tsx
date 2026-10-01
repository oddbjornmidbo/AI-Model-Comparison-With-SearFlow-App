import type { Meta, StoryObj } from '@storybook/react-vite'
import { registrations } from '../fixtures'
import { RegistrationList } from './RegistrationList'

const meta = {
  title: 'Components/RegistrationList',
  component: RegistrationList,
  args: { registrations, onCancel: () => {} },
} satisfies Meta<typeof RegistrationList>
export default meta
type Story = StoryObj<typeof meta>

export const Populated: Story = {}
export const Cancelling: Story = { args: { cancellingId: 12 } }
export const ConfirmedOnly: Story = { args: { registrations: registrations.slice(0, 2) } }
export const Empty: Story = { args: { registrations: [] } }
export const Loading: Story = { args: { registrations: undefined, loading: true } }
export const Refreshing: Story = { args: { loading: true } }
export const Error: Story = { args: { registrations: undefined, error: 'Workshop not found.', onRetry: () => {} } }
