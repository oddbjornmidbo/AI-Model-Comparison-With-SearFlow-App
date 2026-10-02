import type { Meta, StoryObj } from '@storybook/react-vite'
import { participants, registrations, workshops } from '../fixtures'
import { WorkshopDetail } from './WorkshopDetail'

const meta = {
  title: 'Components/WorkshopDetail',
  component: WorkshopDetail,
  args: {
    workshop: workshops[1],
    registrations,
    participants,
    onRegister: () => {},
    onCancel: () => {},
  },
} satisfies Meta<typeof WorkshopDetail>
export default meta
type Story = StoryObj<typeof meta>

export const FullWithWaitlist: Story = {}
export const SeatsAvailableNoRegistrations: Story = { args: { workshop: workshops[2], registrations: [] } }
export const Submitting: Story = { args: { submitting: true } }
export const LoadingRegistrations: Story = { args: { registrations: undefined, registrationsLoading: true } }
export const RegistrationsError: Story = {
  args: { registrations: undefined, registrationsError: 'Could not load registrations.', onRetryRegistrations: () => {} },
}
