import type { Meta, StoryObj } from '@storybook/react-vite'
import { StatusBadge } from './StatusBadge'

const meta = { title: 'Components/StatusBadge', component: StatusBadge } satisfies Meta<typeof StatusBadge>
export default meta
type Story = StoryObj<typeof meta>

export const Confirmed: Story = { args: { status: 'Confirmed' } }
export const Waitlisted: Story = { args: { status: 'Waitlisted' } }
export const Cancelled: Story = { args: { status: 'Cancelled' } }
export const All: Story = {
  args: { status: 'Confirmed' },
  render: () => (
    <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
      <StatusBadge status="Confirmed" />
      <StatusBadge status="Waitlisted" />
      <StatusBadge status="Cancelled" />
    </div>
  ),
}
